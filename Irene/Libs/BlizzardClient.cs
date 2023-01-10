namespace Irene;

using System.Net.Http;
using System.Text.Json.Nodes;

class BlizzardClient {
	public enum Namespace { Static, Dynamic, Profile }

	private static readonly HttpClient _http = new ();
	private const string
		_urlOauth = @"https://oauth.battle.net/token",
		_urlApi = @"https://us.api.blizzard.com/";
	private const string
		_locale = @"locale=en_US",
		_namespaceStatic  = @"namespace=static-us" ,
		_namespaceDynamic = @"namespace=dynamic-us",
		_namespaceProfile = @"namespace=profile-us";
	private const string
		_keyToken = @"access_token",
		_keyExpiry = @"expires_in";

	public bool IsConnected =>
		(_token is not null)
		&& (_tokenExpiry is not null)
		&& (DateTimeOffset.UtcNow < _tokenExpiry);

	private readonly string _clientId;
	private readonly string _clientSecret;
	private string? _token = null;
	private DateTimeOffset? _tokenExpiry = null;

	static BlizzardClient() {
		_http.BaseAddress = new (_urlApi);
	}

	public BlizzardClient(string clientId, string clientSecret) {
		_clientId = clientId;
		_clientSecret = clientSecret;
	}

	// Fetch an authorization token from the Blizzard API.
	// (These are valid for 24 hours.)
	public async Task ConnectAsync() {
		// Construct a request to fetch an authorization token.
		using HttpRequestMessage request = new (
			HttpMethod.Post,
			_urlOauth + @"?grant_type=client_credentials"
		);

		// Properly encode and attach the request authorization.
		byte[] tokenBytes =
			Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}");
		string token = Convert.ToBase64String(tokenBytes);
		request.Headers.Authorization = new ("Basic", token);

		// Send and wait for a successful response.
		using HttpResponseMessage response =
			await _http.SendAsync(request);
		if (!response.IsSuccessStatusCode) {
			Log.Error("Blizzard API authorization failure: {StatusCode}", response.StatusCode);
			Log.Information("  {Reason}", response.ReasonPhrase);
			throw new NetworkException(_urlOauth);
		}

		// Parse and extract data from the response.
		try {
			string content = await
				response.Content.ReadAsStringAsync();
			JsonNode parser = JsonNode.Parse(content)
				?? throw new FormatException();

			_token = Util.ParseString(parser, _keyToken);
			int expirySeconds = Util.ParseInt(parser, _keyExpiry);
			_tokenExpiry = DateTimeOffset.UtcNow
				+ TimeSpan.FromSeconds(expirySeconds);
		} catch (FormatException) {
			throw new NetworkException("Blizzard API authorization");
		}

		// Update the client's default authorization header.
		_http.DefaultRequestHeaders.Authorization =
			new ("Bearer", _token);
	}

	// Make a request from the Blizzard API.
	// Fetches an authorization token if a valid one isn't found.
	public async Task<string> RequestAsync(Namespace @namespace, string url) {
		if (!IsConnected)
			await ConnectAsync();

		string namespaceString = @namespace switch {
			Namespace.Static  => _namespaceStatic ,
			Namespace.Dynamic => _namespaceDynamic,
			Namespace.Profile => _namespaceProfile,
			_ => throw new UnclosedEnumException(typeof(Namespace), @namespace),
		};

		string result = await
			_http.GetStringAsync($"{url}?{namespaceString}&{_locale}");

		return result;
	}
}
