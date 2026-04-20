namespace Snippets.Sdk
{
    /// <summary>
    /// Controls how snippet text is presented during playback.
    /// </summary>
    public enum SnippetTextDisplayMode
    {
        FullText = 0,
        HighlightAsSpoken = 1,
        BuildUpText = 2,
        RollingSubtitles = 3,
        ScreenSubtitles = 4,
        Typewriter = 5,
        TwoLineSubtitles = 6
    }
}
