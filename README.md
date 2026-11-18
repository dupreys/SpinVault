# SpinVault para Android

**SpinVault** é o catálogo da sua coleção física de **vinis, CDs e fitas cassete**. Esta é a versão para **Android** (.NET MAUI), companheira do SpinVault para Windows. Os dados ficam no seu celular, em um banco SQLite local. A nuvem é opcional.

<p align="center">
  <img alt="plataforma" src="https://img.shields.io/badge/plataforma-Android%207%2B-3DDC84">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10-512BD4">
  <img alt="idiomas" src="https://img.shields.io/badge/idiomas-Portugu%C3%AAs%20%7C%20English-34D399">
</p>

## Funcionalidades
- Coleção em galeria de capas, com busca e filtro por mídia.
- Lista de desejos, com o botão **Comprei!**.
- Detalhes, edição e exclusão de álbuns, com capa pela galeria ou da internet e busca de dados no MusicBrainz.
- Resumo da coleção: valor investido, ticket médio, gasto por ano, artistas mais frequentes e aniversários do mês.
- **Nuvem:** login com o Google e backup automático em uma pasta oculta do seu Drive; ao entrar em outro aparelho, a coleção é restaurada.
- Baixar capas e completar datas de lançamento de todos os álbuns de uma vez.
- Importar Excel, CSV ou CSV do Discogs. Exportar para Excel ou CSV. Backup e restauração em .zip.
- Português e English. Tema escuro e claro.

## Instalação
1. Abra a página de [Releases](../../releases) e baixe o arquivo `SpinVault-x.y.z.apk` no celular.
2. Abra o arquivo. Se o Android pedir, permita **instalar apps de fontes desconhecidas** para o seu navegador ou gerenciador de arquivos.
3. Abra o SpinVault.

O app não está na Play Store.

## Nuvem (Google Drive)
Em **Configurações → Nuvem (Google Drive)**, toque em **Entrar com o Google**. O app só acessa a **pasta oculta dele mesmo** no seu Drive (permissão `drive.appdata`) e não vê nenhum outro arquivo. Ele envia o backup quando você sai do app e confere a nuvem quando abre. Nunca troca uma coleção por outra sem perguntar, a não ser que o aparelho não tenha alterações novas.

> Enquanto o app do Google estiver em modo de teste, só os e-mails cadastrados como usuários de teste conseguem entrar. Veja "Desenvolvimento" abaixo.

## Desenvolvimento
Requisitos: Visual Studio 2022 17.14 (ou 2026) com a carga **.NET MAUI** e o **.NET 10 SDK**.

1. Abra `SpinVault.Android.csproj`, escolha um emulador (com Play Store) ou um celular com depuração USB e rode em Debug.
2. `Shared/` é uma cópia do código de dados, importação, exportação, backup, idioma e traduções do SpinVault para Windows. O formato do banco e do backup é o mesmo nos dois.
3. **Login com o Google:** crie um projeto no Google Cloud, ative a Drive API, configure a tela de consentimento OAuth (escopo `drive.appdata`) e crie um ID de cliente **Android** com o nome do pacote `com.spinvault.app` e o SHA-1 da chave que assina o app, com o esquema de URI personalizado habilitado. Há um ID para Debug e outro para Release (`Cloud/GoogleAuth.cs`); os dois esquemas de retorno também estão em `Platforms/Android/WebAuthCallbackActivity.cs`.

### Gerar o APK
```powershell
dotnet publish SpinVault.Android.csproj -c Release -f net10.0-android `
  -p:AndroidKeyStore=true -p:AndroidSigningKeyStore=spinvault.keystore `
  -p:AndroidSigningKeyAlias=spinvault -p:AndroidSigningKeyPass=env:KS_PASS `
  -p:AndroidSigningStorePass=env:KS_PASS -p:PublishTrimmed=false -p:RunAOTCompilation=false
```
Ou pelo GitHub Actions (workflow **Gerar APK do Android**) ao criar uma tag `v*`. Para o Android aceitar atualizações por cima e o login com o Google continuar funcionando, o APK precisa ser sempre assinado com a **mesma chave** (o SHA-1 cadastrado no Google). Guarde o keystore e configure os segredos `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD` e `ANDROID_KEY_ALIAS` no repositório. Nunca suba o keystore para o Git.

## Licença
Defina a licença antes de tornar o repositório público (por exemplo MIT). Sem licença, o código fica com todos os direitos reservados ao autor.

## Créditos
Dados de álbuns: [MusicBrainz](https://musicbrainz.org). Capas: [Cover Art Archive](https://coverartarchive.org).
