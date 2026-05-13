// we will setup offline and online authentication methods here
// online will use firebase auth and offline will use localstorage
//
// Canonical spec: Assets/Docs/25 Authentication, Networking, and Data Lifecycle.md
// Project section: Assets/Authentication-Networking/README.md
// Init: cloud (Firebase OK?), steam, local, steam_authenticated, steam_auth_error, signed_in_cloud (derived).
// Steam path: Cloud Function "steam_login" then Firebase signInWithEmailAndPassword( "<steam_id>@steam.link", serverCredential )
// If !cloud && steam: no steam_login; Steam lobbies/MP still on. local=true only for !cloud && !steam (or sandbox).
// Implementation scaffold: Authentication-Networking/Runtime (FirebaseDriver, AuthSessionCoordinator) and UI/LoginScreenUiController.cs
