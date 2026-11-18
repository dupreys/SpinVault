using ColecaoMidias.Models;
using SpinVaultMobile.ViewModels;

namespace SpinVaultMobile;

public partial class BatchPage : ContentPage
{
    private readonly BatchViewModel _vm;

    public BatchPage(bool covers, IEnumerable<Album> albums)
    {
        InitializeComponent();
        _vm = new BatchViewModel(covers, albums);
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        DeviceDisplay.Current.KeepScreenOn = true;   // não deixa a tela apagar durante a busca
        await _vm.RunAsync();
        DeviceDisplay.Current.KeepScreenOn = false;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _vm.Cancel();
        DeviceDisplay.Current.KeepScreenOn = false;
    }

    protected override bool OnBackButtonPressed()
    {
        _vm.Cancel();
        return base.OnBackButtonPressed();
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        if (_vm.IsRunning)
            _vm.Cancel();
        else
            await Navigation.PopModalAsync();
    }
}
