using SpinVaultMobile.ViewModels;

namespace SpinVaultMobile;

public partial class SummaryPage : ContentPage
{
    private readonly SummaryViewModel _vm = new();

    public SummaryPage()
    {
        InitializeComponent();
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.Reload();
    }
}
