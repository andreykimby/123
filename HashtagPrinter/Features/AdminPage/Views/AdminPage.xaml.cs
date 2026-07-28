using Skarynagram.Features.AdminPage.ViewModels;

namespace Skarynagram.Features.AdminPage.Views
{
    public partial class AdminPage : ContentPage
    {
        public AdminPage(AdminPageViewModel adminPageViewModel) : this()
        {
            BindingContext = adminPageViewModel;
        }

        public AdminPage()
        {
            InitializeComponent();
            
        }



        
    }
}
