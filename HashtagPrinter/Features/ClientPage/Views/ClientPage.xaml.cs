using CommunityToolkit.Mvvm.Messaging;
using Skarynagram.Features.InstaViewer.Messages;
using Skarynagram.Features.ViewModels;
using MauiPageFullScreen;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using Windows.UI.ViewManagement;
using WinRT.Interop;

namespace Skarynagram.Features.ClientPage.Views;

[XamlCompilation(XamlCompilationOptions.Compile)]
public partial class ClientPage : ContentPage
{
    private MauiWinUIWindow? _nativeWindow;
    public bool tsts;
    public ClientPage(ClientPageViewModel clientPageViewModel) : this()
    {
        BindingContext = clientPageViewModel;
        clientPageViewModel.View = this.Content;
        clientPageViewModel.RequestCloseClientWindow += () =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Keep the MAUI window alive and hide only its native Windows window.
                // Destroying the secondary MAUI window can terminate this WinUI process.
                if (this.Window?.Handler?.PlatformView is MauiWinUIWindow nativeWindow)
                    nativeWindow.AppWindow.Hide();
            });
        };

        

    }

    public ClientPage()
	{
        InitializeComponent();

    }
    
    protected override void OnParentSet()
    {
        base.OnParentSet();
        if (BindingContext is ClientPageViewModel vm && this.Window != null)
        {
            vm.SetWindow(this.Window);
        }
    }

    

    private void WindowMaximize(object sender, EventArgs e)
    {
        buttonMin.IsVisible = false;
        buttonMax.IsVisible = false;
        MaximizeWindow();
    }

    private void WindowRestore(object sender, EventArgs e)
    {
        RestoreWindow();
    }

    

    public void MaximizeWindow()
    {
        var nativeWindow = Application.Current?.Windows?
            .FirstOrDefault(w => w.Page == this)?
            .Handler?
            .PlatformView as MauiWinUIWindow;
        if (nativeWindow == null)
            return;
        var appWindow = nativeWindow.AppWindow;
        appWindow.SetPresenter(AppWindowPresenterKind.FullScreen);

    }

    public void RestoreWindow()
    {
        var nativeWindow = Application.Current?.Windows?.FirstOrDefault(w => w.Page == this)?.Handler.PlatformView as MauiWinUIWindow;
        if (nativeWindow == null)
            return;
        var appWindow = nativeWindow.AppWindow;
        appWindow.SetPresenter(AppWindowPresenterKind.Default);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        // ����� ����������� �� ����� handler'� ���� �����
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // OnParentSet fires before WinUI has necessarily created the native window.
        // Switching presenters at that point intermittently terminates Microsoft.UI.Xaml.
        Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(300), MaximizeWindow);
        WeakReferenceMessenger.Default.Send(new FetchLatestMediaMessage(string.Empty));
    }

    

    private void MediaCollection_Scrolled(object sender, ItemsViewScrolledEventArgs e)
    {
        
    }

}
