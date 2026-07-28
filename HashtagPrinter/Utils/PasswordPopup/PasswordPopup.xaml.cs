using CommunityToolkit.Maui.Views;

namespace Utils.PasswordPopup;

public partial class PasswordPopup : Popup
{

	public PasswordPopup()
	{
		
		InitializeComponent();
	}

	public PasswordPopup(PasswordPopupViewModel passwordPopupViewModel) : this()
	{
		BindingContext = passwordPopupViewModel;
    }

}