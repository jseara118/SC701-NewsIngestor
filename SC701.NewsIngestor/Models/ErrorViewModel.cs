namespace SC701_NewsIngestor.Models

//comment: This model represents the error information displayed in the error view.
{
    public class ErrorViewModel
    {
        public string? RequestId { get; set; }

        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}