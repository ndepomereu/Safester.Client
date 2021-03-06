﻿using Acr.UserDialogs;
using Safester.Controls;
using Safester.CryptoLibrary.Api;
using Safester.Custom.Effects;
using Safester.Network;
using Safester.Services;
using Safester.Utils;
using System;
using System.Collections.Generic;
using Xamarin.Forms;

namespace Safester.Views
{
    public partial class LoginPage : ContentPage
    {
        public static string CurrentUserEmail { get; set; }
        public static string CurrentUserPassword { get; set; }
        public static bool NeedsUpdating { get; set; }
        bool rememberUser = false;

        private SettingsService _settingsService { get; set; }
        private bool _isMultiAccount { get; set; }

        public LoginPage(bool isMultiAccount = false)
        {
            InitializeComponent();

            imgLogo.Source = ImageSource.FromFile(ThemeHelper.GetLoginLogoName());

            _isMultiAccount = isMultiAccount;
            if (!isMultiAccount)
                NavigationPage.SetHasNavigationBar(this, false);

            TapGestureRecognizer loginGesture = new TapGestureRecognizer();
			loginGesture.Tapped += StartBtn_Clicked;
			ImgLogin.GestureRecognizers.Add(loginGesture);

            TapGestureRecognizer signupGesture = new TapGestureRecognizer();
            signupGesture.Tapped += SignupGesture_Tapped;
            lblSignUp.GestureRecognizers.Add(signupGesture);

            TapGestureRecognizer rememberGesture = new TapGestureRecognizer();
            rememberGesture.Tapped += RememberGesture_Tapped;
            stackRemember.GestureRecognizers.Add(rememberGesture);

            _settingsService = DependencyService.Get<SettingsService>();
			lblVersion.Text = AppResources.Version + _settingsService.GetAppVersionName();

            var isFirstRun = _settingsService.LoadSettings("app_first_run");
            if (string.IsNullOrEmpty(isFirstRun))
            {
                _settingsService.SaveSettings("app_first_run", "1");
                _settingsService.SaveSettings("mobile_signature", (Device.RuntimePlatform == Device.Android) ? AppResources.SentFromAndroid : AppResources.SentFromiOS);
            }

            rememberUser = _settingsService.LoadSettings("rememberuser").Equals("1");
            if (rememberUser && NeedsUpdating == false)
            {
                CurrentUserEmail = entryUserName.Text = _settingsService.LoadSettings("useremail");
                string passwordSetting = _settingsService.LoadSettings("password");
                string encrypted = _settingsService.LoadSettings("password_encrypted");
                if (string.IsNullOrEmpty(encrypted) == false && encrypted.Equals("1"))
                    passwordSetting = Utils.Utils.GetDecryptedPassphrase(passwordSetting);

                CurrentUserPassword = entryPassword.Text = passwordSetting;
            }

            entryPassword.Effects.Add(new ShowHidePassEffect());
            ChangeUIOption();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();

            BackgroundColor = ThemeHelper.GetLoginBGColor();

            if (NeedsUpdating)
            {
                entryUserName.Text = CurrentUserEmail;
                entryPassword.Text = CurrentUserPassword;
            }

            NeedsUpdating = false;
        }

        async void StartBtn_Clicked(object sender, EventArgs e)
		{
            if (string.IsNullOrEmpty(entryUserName.Text))
            {
                await CustomAlertPage.Show(AppResources.Warning, AppResources.ErrorInputUserName, AppResources.OK);
                return;
            }
            if (string.IsNullOrEmpty(entryPassword.Text))
            {
                await CustomAlertPage.Show(AppResources.Warning, AppResources.ErrorInputUserPassPhrase, AppResources.OK);
                return;
            }

            ShowLoading(true);
            ApiManager.SharedInstance().Login(entryUserName.Text, entryPassword.Text, "", (success, message) =>
            {
                if (success == true)
                {
                    App.CurrentUser.UserEmail = entryUserName.Text;
                    App.CurrentUser.UserPassword = entryPassword.Text.ToCharArray();

                    ApiManager.SharedInstance().GetPrivateKey(App.CurrentUser.UserEmail, App.CurrentUser.Token, (suc, keyInfo) => {
                        ShowLoading(false);
                        if (suc)
                        {
                            App.CurrentUser.PrivateKey = keyInfo.privateKey;
                            App.KeyDecryptor = new Decryptor(App.CurrentUser.PrivateKey, App.CurrentUser.UserPassword);

                            _settingsService.SaveSettings("rememberuser", rememberUser ? "1" : "0");
                            if (rememberUser)
                            {
                                _settingsService.SaveSettings("useremail", entryUserName.Text);
                                _settingsService.SaveSettings("password_encrypted", "1");
                                _settingsService.SaveSettings("password", Utils.Utils.GetEncryptedPassphrase(entryPassword.Text));
                            }

                            Utils.Utils.AddOrUpdateUser(App.CurrentUser);
                            Utils.Utils.SaveDataToFile(App.LocalUsers, Utils.Utils.KEY_FILE_USERS);

                            Utils.Utils.LoadUserProfiles();

                            App.Current.MainPage = new NavigationPage(new MainPage());
                        }
                        else
                        {
                            CustomAlertPage.Show(AppResources.Error, keyInfo.errorMessage, AppResources.OK);
                        }
                    });
                }
                else
                {
                    ShowLoading(false);

                    if (message.StartsWith(Errors.LOGIN_ACCOUNT_PENDING, StringComparison.OrdinalIgnoreCase))
                    {
                        var alertMsg = AppResources.ALERT_CONFIRM_EMAIL.Replace("\\n", "\n");
                        CustomAlertPage.Show("", string.Format(alertMsg, entryUserName.Text), AppResources.OK);
                    }
                    else if (message.StartsWith(Errors.LOGIN_ACCOUNT_INVALID2FA, StringComparison.OrdinalIgnoreCase))
                    {
                        Navigation.PushAsync(new TwoFactorPage(entryUserName.Text, entryPassword.Text, rememberUser));
                    }
                    else
                    {
                        CustomAlertPage.Show(AppResources.Error, message, AppResources.OK);
                    }
                }
            });
		}

		void SignupGesture_Tapped(object sender, EventArgs e)
		{
            Navigation.PushAsync(new PatatePage());
		}

		void RememberGesture_Tapped(object sender, EventArgs e)
		{
            rememberUser = !rememberUser;
            ChangeUIOption();
		}

        void ChangeUIOption()
        {
            imgRemember.Source = ImageSource.FromFile(rememberUser ? "check_box_checked_small.png" : "check_box_uncheck_small.png");            
        }

        private void ShowLoading(bool isShowing)
        {
            if (isShowing)
                UserDialogs.Instance.Loading(AppResources.Pleasewait, null, null, true);
            else
                UserDialogs.Instance.Loading().Hide();
        }
    }
}
