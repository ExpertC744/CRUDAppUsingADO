namespace CRUDAppUsingADO.Models.Attendance
{
    public sealed class EventGalleryImageDto
    {
        public long GalleryImageId { get; set; }

        public int EventId { get; set; }

        public string ImageName { get; set; } = "";

        public string ImagePath { get; set; } = "";

        public DateTime CreatedAt { get; set; }
    }
}
