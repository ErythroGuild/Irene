namespace Irene;

class PhrasePool {
	public IList<string> Templates { get; }
	public object[] Data { get; }

	public PhrasePool(IList<string> templates, params object[] data) {
		Templates = templates;
		Data = data;
	}

	public string Random() {
		int i = System.Random.Shared.Next(0, Templates.Count);
		return string.Format(Templates[i], Data);
	}
}
