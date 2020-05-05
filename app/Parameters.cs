namespace LargeFileFiller
{
    public class Parameters
    {
        public string FileName { get; set; }
        public long FileSize { get; set; }
        public SizeUnit FileSizeUnit { get; set; }
        public ContentFillType ContentFill { get; set; }
        public string ContentTemplate { get; set; }
        public bool AppendContent { get; set; }
        public bool ShowHelp { get; set; }
        public bool HideBanner { get; set; }
        public bool RunSilently { get; set; }
        public bool Verbose { get; set; }
    }
}
