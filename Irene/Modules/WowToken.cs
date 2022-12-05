namespace Irene.Modules;

using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;

class WowToken {
	public enum Region { US, EU, KR, TW, CN }

	private record struct Data(
		int PriceCurrent,
		int Price1dHigh , int Price1dLow ,
		int Price7dHigh , int Price7dLow ,
		int Price30dHigh, int Price30dLow,
		DateTimeOffset TimeUpdated,
		int PriceTrend
	);

	private static readonly HttpClient _client = new ();

	// Parsing configuration.
	private const string _urlFeed = @"https://wowtokenprices.com/current_prices.json";
	private const int _maxJsonDepth = 5;
	private const string
		_keyPriceCurrent = "current_price",
		_keyPrice1dHigh  =  "1_day_high", _keyPrice1dLow  =  "1_day_low",
		_keyPrice7dHigh  =  "7_day_high", _keyPrice7dLow  =  "7_day_low",
		_keyPrice30dHigh = "30_day_high", _keyPrice30dLow = "30_day_low",
		_keyTimeUpdated  = "time_of_last_change_unix_epoch",
		_keyPriceTrend = "last_change";

	// Display constants.
	private const string
		_trendUp   = "\u25B2",
		_trendDown = "\u25BC",
		_trendFlat = "\u2014";
	private const string _ensp = "\u2002";
	private const string _urlSource = @"https://wowtokenprices.com/";
	private const string _footerSource = @"wowtokenprices.com";
	private static readonly DiscordColor _colorBlizzard = new ("#00CCFF");

	// Return a formatted display of the selected region's prices.
	// Returns null if prices could not be fetched.
	public static async Task<DiscordEmbed?> DisplayPrices(Region region) {
		// Fetch and parse data.
		string json = await _client.GetStringAsync(_urlFeed);
		Data? data = ParseData(json, region);

		// Return null if JSON parsing failed.
		if (data is null)
			return null;

		// Format data for display.
		string trend = data.Value.PriceTrend switch {
			>0 => _trendUp  ,
			<0 => _trendDown,
			 _ => _trendFlat,
		};
		string content =
			$"""
			**{region} Price History**
			`{FormatPrice(data.Value.Price1dLow )}` ~ `{FormatPrice(data.Value.Price1dHigh )}`{_ensp}- 1 day
			`{FormatPrice(data.Value.Price7dLow )}` ~ `{FormatPrice(data.Value.Price7dHigh )}`{_ensp}- 7 day
			`{FormatPrice(data.Value.Price30dLow)}` ~ `{FormatPrice(data.Value.Price30dHigh)}`{_ensp}- 30 day
			""";

		DiscordEmbedBuilder embed =
			new DiscordEmbedBuilder()
			.WithTitle($"{trend} {data.Value.PriceCurrent:N0} :coin:")
			.WithUrl(_urlSource)
			.WithColor(_colorBlizzard)
			.WithDescription(content)
			.WithFooter(_footerSource)
			.WithTimestamp(data.Value.TimeUpdated);

		return embed.Build();
	}

	// Round prices to the nearest 1000 (display as e.g. "120k").
	private static string FormatPrice(int price) {
		string output = Math.Round(price / 1000.0).ToString();
		return $"{output}k";
	}

	// Parse the JSON returned from an HTTP request to the data feed.
	// Returns null if there were any parsing errors.
	private static Data? ParseData(string json, Region region) {
		// Parse JSON and extract the data for the selected region.
		JsonDocumentOptions parseOptions =
			new () { MaxDepth = _maxJsonDepth };
		JsonNode? root = null;
		try {
			root = JsonNode.Parse(json, null, parseOptions);
		}
		catch (ArgumentException) { } // json document was malformed
		catch (JsonException)     { } // parsing failed

		// Return null if any errors occur.
		if (root is null)
			return null;
		JsonNode? data = root[GetKey(region)];
		if (data is null)
			return null;

		// Convenience functions for throwing on null results.
		static int ParseInt(JsonNode node, string key) =>
			node[key]?.GetValue<int>() ?? throw new FormatException();
		static long ParseLong(JsonNode node, string key) =>
			node[key]?.GetValue<long>() ?? throw new FormatException();

		// Populate data from deserialized JSON object.
		try {
			int priceCurrent = ParseInt(data, _keyPriceCurrent);
			int price1dHigh  = ParseInt(data, _keyPrice1dHigh );
			int price1dLow   = ParseInt(data, _keyPrice1dLow  );
			int price7dHigh  = ParseInt(data, _keyPrice7dHigh );
			int price7dLow   = ParseInt(data, _keyPrice7dLow  );
			int price30dHigh = ParseInt(data, _keyPrice30dHigh);
			int price30dLow  = ParseInt(data, _keyPrice30dLow );

			long timeRaw = ParseLong(data, _keyTimeUpdated);
			DateTimeOffset timeUpdated =
				DateTimeOffset.FromUnixTimeSeconds(timeRaw);

			int priceTrend = ParseInt(data, _keyPriceTrend);

			return new (
				priceCurrent,
				price1dHigh , price1dLow ,
				price7dHigh , price7dLow ,
				price30dHigh, price30dLow,
				timeUpdated,
				priceTrend
			);
		} catch (FormatException) {
			// Return null whenever parsing fails.
			return null;
		}
	}

	// Fetch the JSON key used to parse each region's data.
	private static string GetKey(Region region) => region switch {
		Region.US => "us"    ,
		Region.EU => "eu"    ,
		Region.KR => "korea" ,
		Region.TW => "taiwan",
		Region.CN => "china" ,
		_ => throw new UnclosedEnumException(typeof(Region), region),
	};
}
