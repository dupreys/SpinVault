using Android.App;
using Android.Content;
using Android.Content.PM;
using SpinVaultMobile.Cloud;

namespace SpinVaultMobile;

/// <summary>Recebe a volta do login do Google (o navegador chama o app por este endereço).</summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "com.googleusercontent.apps.108914129021-hbc426r2besd5j2l6emn7hs91vkg4f9o",
    DataPath = "/oauth2redirect")]
[IntentFilter(new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "com.googleusercontent.apps.108914129021-ara0ld7pjv7aokevi20ldp14vq29tbqc",
    DataPath = "/oauth2redirect")]
public class WebAuthCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
}
