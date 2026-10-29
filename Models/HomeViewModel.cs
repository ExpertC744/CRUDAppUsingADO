using System.Collections.Generic;

namespace CRUDAppUsingADO.Models
{
    public class HomeViewModel
    {
        public FeedbackViewModel? AdminFeedback { get; set; }

        public List<FeedbackViewModel> OtherFeedback { get; set; }
            = new List<FeedbackViewModel>();
    }
}