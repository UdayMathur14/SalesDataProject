namespace SalesDataProject.Models
{

    public class ValidationResultViewModel
    {
        public List<TitleValidationViewModel> BlockedTitles { get; set; }
        public List<TitleValidationViewModel> CleanTitles { get; set; } 

        public ValidationResultViewModel()
        {
            BlockedTitles = new List<TitleValidationViewModel>();
            CleanTitles = new List<TitleValidationViewModel>();
        }
    }

}
