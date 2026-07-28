using CommunityToolkit.Maui;
using CommunityToolkit.Maui.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Skarynagram.Features.AppSettingsService.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Utils.PasswordPopup
{
    public partial class PasswordPopupViewModel : ObservableObject, IQueryAttributable
    {
        [ObservableProperty]
        public partial string EnteredPassword { get; set; } = string.Empty;
        
        
        [ObservableProperty]
        public partial Page? ParentPage { get; set; }
        readonly IPopupService _popupService;

        public PasswordPopupViewModel(IPopupService popupService)
        {
            _popupService = popupService;
        }

        [RelayCommand]
        async Task OnCancel()
        {
            
            if (ParentPage != null)
                await _popupService.ClosePopupAsync(ParentPage);
            else
                // fallback: попробуй Shell.Current или Application.Current.MainPage
                
                await _popupService.ClosePopupAsync(Shell.Current ?? Application.Current?.MainPage);

            //await _popupService.ClosePopupAsync(Shell.Current);

        }

        partial void OnEnteredPasswordChanged(string value)
        {
            SaveCommand.NotifyCanExecuteChanged();
        }


        [RelayCommand(CanExecute = nameof(CanSave))]
        async Task OnSave()
        {
            await _popupService.ClosePopupAsync(ParentPage, EnteredPassword);
        }

        bool CanSave() => EnteredPassword.Length>0;

        public void ApplyQueryAttributes(IDictionary<string, object> query)
        {
            ParentPage = (Page)query[nameof(PasswordPopupViewModel.ParentPage)];
        }
    }
}
