﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Rest;
using Microsoft.Rest.Azure.Authentication.Properties;

namespace Microsoft.Rest.Azure.Authentication
{
    /// <summary>
    /// Provides tokens for Azure Active Directory applications. 
    /// </summary>
    public class ApplicationTokenProvider : ITokenProvider
    {
        private AuthenticationContext _authenticationContext;
        private string _tokenAudience;
        private IApplicationAuthenticationProvider _authentications;
        private string _clientId;
        private DateTimeOffset _expiration;
        private string _accessToken;
        private string _accessTokenType;
        private static readonly TimeSpan ExpirationThreshold = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Create an application token provider that can retrieve tokens for the given application from the given context, using the given audience 
        /// and credential.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="context">The authentication context to use when retrieving tokens.</param>
        /// <param name="tokenAudience">The token audience to use when retrieving tokens.</param>
        /// <param name="credential">The client credential for this application.</param>
        /// <param name="authenticationResult">The token details provided when authenticating with the client credentials.</param>
        public ApplicationTokenProvider(AuthenticationContext context, string tokenAudience, ClientCredential credential, AuthenticationResult authenticationResult)
        {
            if (credential == null)
            {
                throw new ArgumentNullException("credential");
            }

            if (authenticationResult == null)
            {
                throw new ArgumentNullException("authenticationResult");
            }

            Initialize(context, tokenAudience, credential.ClientId, new MemoryApplicationAuthenticationProvider(credential), authenticationResult, authenticationResult.ExpiresOn);
        }

        /// <summary>
        /// Create an application token provider that can retrieve tokens for the given application from the given context, using the given audience 
        /// and certificate.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="context">The authentication context to use when retrieving tokens.</param>
        /// <param name="tokenAudience">The token audience to use when retrieving tokens.</param>
        /// <param name="certificate">The certificate associated with Active Directory application.</param>
        /// <param name="authenticationResult">The token details provided when authenticating with the client credentials.</param>
        public ApplicationTokenProvider(AuthenticationContext context, string tokenAudience, ClientAssertionCertificate certificate, AuthenticationResult authenticationResult)
        {
            if (certificate == null)
            {
                throw new ArgumentNullException("certificate");
            }

            if (authenticationResult == null)
            {
                throw new ArgumentNullException("authenticationResult");
            }

            Initialize(context, tokenAudience, certificate.ClientId, 
                new CertificateAuthenticationProvider((clientId) => Task.FromResult(certificate)),
                authenticationResult, authenticationResult.ExpiresOn);
        }

        /// <summary>
        /// Create an application token provider that can retrieve tokens for the given application from the given context, using the given audience 
        /// and credential store.
         /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
       /// </summary>
        /// <param name="context">The authentication context to use when retrieving tokens.</param>
        /// <param name="tokenAudience">The token audience to use when retrieving tokens</param>
        /// <param name="clientId">The client Id for this active directory application</param>
        /// <param name="authenticationStore">The source of authentication information for this application.</param>
        /// <param name="authenticationResult">The authenticationResult of initial authentication with the application credentials.</param>
        public ApplicationTokenProvider(AuthenticationContext context, string tokenAudience, string clientId,
             IApplicationAuthenticationProvider authenticationStore, AuthenticationResult authenticationResult)
        {
            if (authenticationResult == null)
            {
                throw new ArgumentNullException("authenticationResult");
            }

            Initialize(context, tokenAudience, clientId, authenticationStore, authenticationResult, authenticationResult.ExpiresOn);
        }

        /// <summary>
        /// Create an application token provider that can retrieve tokens for the given application from the given context, using the given audience 
        /// and credential store.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="context">The authentication context to use when retrieving tokens.</param>
        /// <param name="tokenAudience">The token audience to use when retrieving tokens</param>
        /// <param name="clientId">The client Id for this active directory application</param>
        /// <param name="authenticationStore">The source of authentication information for this application.</param>
        /// <param name="authenticationResult">The authenticationResult of initial authentication with the application credentials.</param>
        /// <param name="tokenExpiration">The date of expiration for the current access token.</param>
        public ApplicationTokenProvider(AuthenticationContext context, string tokenAudience, string clientId,
            IApplicationAuthenticationProvider authenticationStore, AuthenticationResult authenticationResult, DateTimeOffset tokenExpiration)
        {
            Initialize(context, tokenAudience, clientId, authenticationStore, authenticationResult, tokenExpiration);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application using client credentials. Uses the default token cache and the default 
        /// service settings (authority, token audience) for log in to azure resource manager during authentication.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="clientId">The active directory clientId for the application.</param>
        /// <param name="secret">The secret for this active directory application.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, string clientId, string secret)
        {
            return await LoginSilentAsync(domain, clientId, secret, ActiveDirectoryServiceSettings.Azure, TokenCache.DefaultShared);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application using certificate credentials. Uses the default token cache and the default 
        /// service settings (authority, token audience) for log in to azure resource manager during authentication.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="clientId">The active directory clientId for the application.</param>
        /// <param name="certificate">The certificate associated with Active Directory application.</param>
        /// <param name="password">The certificate password.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, string clientId, byte[] certificate, string password)
        {
            return await LoginSilentAsync(domain, clientId, certificate, password, ActiveDirectoryServiceSettings.Azure, TokenCache.DefaultShared);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application using client credentials. Uses the default service settings 
        /// (authority, token audience) for log in to azure resource manager during authentication.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="clientId">The active directory clientId for the application.</param>
        /// <param name="secret">The secret for this active directory application.</param>
        /// <param name="cache">The token cache to target during authentication.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, string clientId, string secret, TokenCache cache)
        {
            return await LoginSilentAsync(domain, clientId, secret, ActiveDirectoryServiceSettings.Azure, cache);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application using certificate credentials. Uses the default service settings 
        /// (authority, token audience) for log in to azure resource manager during authentication.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="clientId">The active directory clientId for the application.</param>
        /// <param name="certificate">The certificate associated with Active Directory application.</param>
        /// <param name="password">The certificate password.</param>
        /// <param name="cache">The token cache to target during authentication.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, string clientId, byte[] certificate, string password, TokenCache cache)
        {
            return await LoginSilentAsync(domain, clientId, certificate, password, ActiveDirectoryServiceSettings.Azure, cache);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application using client credentials. 
        /// Uses the default token cache during authentication. 
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/"> Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="clientId">The active directory clientId for the application.</param>
        /// <param name="secret">The secret for this active directory application.</param>
        /// <param name="settings">The active directory service side settings, including authority and token audience.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, string clientId, string secret,
            ActiveDirectoryServiceSettings settings)
        {
            return await LoginSilentAsync(domain, clientId, secret, settings, TokenCache.DefaultShared);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application using certificate credentials. 
        /// Uses the default token cache during authentication. 
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/"> Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="clientId">The active directory clientId for the application.</param>
        /// <param name="certificate">The certificate associated with Active Directory application.</param>
        /// <param name="password">The certificate password.</param>
        /// <param name="settings">The active directory service side settings, including authority and token audience.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, string clientId, byte[] certificate, string password,
            ActiveDirectoryServiceSettings settings)
        {
            return await LoginSilentAsync(domain, clientId, certificate, password, settings, TokenCache.DefaultShared);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application using client credentials.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="clientId">The active directory clientId for the application.</param>
        /// <param name="secret">The secret for this active directory application.</param>
        /// <param name="settings">The active directory service side settings, including authority and token audience.</param>
        /// <param name="cache">The token cache to target during authentication.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, string clientId, string secret,
           ActiveDirectoryServiceSettings settings, TokenCache cache)
        {
            return await LoginSilentAsync(domain, new ClientCredential(clientId, secret), settings, cache);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application using certificate credential.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="clientId">The active directory clientId for the application.</param>
        /// <param name="certificate">The certificate associated with Active Directory application.</param>
        /// <param name="password">The certificate password.</param>
        /// <param name="settings">The active directory service side settings, including authority and token audience.</param>
        /// <param name="cache">The token cache to target during authentication.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentAsync(
            string domain, string clientId, byte[] certificate, string password,
           ActiveDirectoryServiceSettings settings, TokenCache cache)
        {
            return await LoginSilentAsync(domain, new ClientAssertionCertificate(clientId, certificate, password), 
                settings, cache);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application using a client credential. Uses the default token cache and the default 
        /// service settings for azure resource manager (authority, token audience) during authentication.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="credential">The client credential (client id and secret) for this active directory application.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, ClientCredential credential)
        {
            return await LoginSilentAsync(domain, credential, ActiveDirectoryServiceSettings.Azure, TokenCache.DefaultShared);
        }
        
        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application using a certificate credential. Uses the default token cache and the default 
        /// service settings for azure resource manager (authority, token audience) during authentication.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="certificate">The certificate associated with Active Directory application.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentWithCertificateAsync(string domain, ClientAssertionCertificate certificate)
        {
            return await LoginSilentAsync(domain, certificate, ActiveDirectoryServiceSettings.Azure, TokenCache.DefaultShared);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application using a client credential. Uses the default service settings 
        /// for azure resource manager (authority, token audience) during authentication.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="credential">The client credential (client id and secret) for this active directory application.</param>
        /// <param name="cache">The token cache to target during authentication.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, ClientCredential credential,
            TokenCache cache)
        {
            return await LoginSilentAsync(domain, credential, ActiveDirectoryServiceSettings.Azure, cache);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application using a certificate credential. Uses the default service settings 
        /// for azure resource manager (authority, token audience) during authentication.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="certificate">The certificate associated with Active Directory application.</param>
        /// <param name="cache">The token cache to target during authentication.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentWithCertificateAsync(string domain, ClientAssertionCertificate certificate,
            TokenCache cache)
        {
            return await LoginSilentAsync(domain, certificate, ActiveDirectoryServiceSettings.Azure, cache);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application using a client credential. Uses the default token cache for authentication.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="credential">The client credential (client id and secret) for this active directory application.</param>
        /// <param name="settings">The active directory service side settings, including authority and token audience.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, ClientCredential credential,
            ActiveDirectoryServiceSettings settings)
        {
            return await LoginSilentAsync(domain, credential, settings, TokenCache.DefaultShared);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application using a certificate credential. Uses the default token cache for authentication.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="certificate">The certificate associated with Active Directory application.</param>
        /// <param name="settings">The active directory service side settings, including authority and token audience.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentWithCertificateAsync(string domain, ClientAssertionCertificate certificate,
            ActiveDirectoryServiceSettings settings)
        {
            return await LoginSilentAsync(domain, certificate, settings, TokenCache.DefaultShared);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application using a client credential.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="credential">The client credential (client id and secret) for this active directory application.</param>
        /// <param name="settings">The active directory service side settings, including authority and token audience.</param>
        /// <param name="cache">The token cache to target during authentication.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, ClientCredential credential,
            ActiveDirectoryServiceSettings settings, TokenCache cache)
        {
            return await LoginSilentAsync(domain, credential.ClientId, new MemoryApplicationAuthenticationProvider(credential),
               settings, cache);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application using a certificate credential.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="certificate">The certificate associated with Active Directory application.</param>
        /// <param name="settings">The active directory service side settings, including authority and token audience.</param>
        /// <param name="cache">The token cache to target during authentication.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
       public static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, ClientAssertionCertificate certificate,
            ActiveDirectoryServiceSettings settings, TokenCache cache)
        {
            return await LoginSilentAsync(domain, certificate.ClientId, 
                new CertificateAuthenticationProvider((clientId) => Task.FromResult(certificate)), settings, cache);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application. Uses the default token cache and default 
        /// service settings (authority and token audience) for authenticating with azure resource manager.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="clientId">The active directory clientId for the application.</param>
        /// <param name="authenticationProvider">A source for the secure secret for this application.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, string clientId,
            IApplicationAuthenticationProvider authenticationProvider)
        {
            return await LoginSilentAsync(domain, clientId, authenticationProvider, ActiveDirectoryServiceSettings.Azure, TokenCache.DefaultShared);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application. Uses the default service settings 
        /// (authority and token audience) for authenticating with azure resource manager.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="clientId">The active directory clientId for the application.</param>
        /// <param name="authenticationProvider">A source for the secure secret for this application.</param>
        /// <param name="cache">The token cache to target during authentication.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, string clientId,
            IApplicationAuthenticationProvider authenticationProvider, TokenCache cache)
        {
            return await LoginSilentAsync(domain, clientId, authenticationProvider, ActiveDirectoryServiceSettings.Azure, cache);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application. Uses the default shared token cache.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="clientId">The active directory clientId for the application.</param>
        /// <param name="authenticationProvider">A source for the secure secret for this application.</param>
        /// <param name="settings">The active directory service side settings, including authority and token audience.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, string clientId,
            IApplicationAuthenticationProvider authenticationProvider, ActiveDirectoryServiceSettings settings)
        {
            return await LoginSilentAsync(domain, clientId, authenticationProvider, settings, TokenCache.DefaultShared);
        }

        /// <summary>
        /// Creates ServiceClientCredentials for authenticating requests as an active directory application.
        /// See <see href="https://azure.microsoft.com/en-us/documentation/articles/active-directory-devquickstarts-dotnet/">Active Directory Quickstart for .Net</see> 
        /// for detailed instructions on creating an Azure Active Directory application.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="clientId">The active directory clientId for the application.</param>
        /// <param name="authenticationProvider">A source for the secure secret for this application.</param>
        /// <param name="settings">The active directory service side settings, including authority and token audience.</param>
        /// <param name="cache">The token cache to target during authentication.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        public static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, string clientId,
            IApplicationAuthenticationProvider authenticationProvider, ActiveDirectoryServiceSettings settings, TokenCache cache)
        {
            var audience = settings.TokenAudience.ToString();
            var context = GetAuthenticationContext(domain, settings, cache);
            var authResult = await authenticationProvider.AuthenticateAsync(clientId, audience, context);
            return new TokenCredentials(new ApplicationTokenProvider(context, audience, clientId,
                    authenticationProvider, authResult));
        }

#if DEBUG
        /// <summary>
        /// For testing purposes only: allows testing token expiration.
        /// </summary>
        /// <param name="domain">The active directory domain or tenantId to authenticate with.</param>
        /// <param name="clientId">The active directory clientId for the application.</param>
        /// <param name="authenticationProvider">A source for the secure secret for this application.</param>
        /// <param name="settings">The active directory service side settings, including authority and token audience.</param>
        /// <param name="cache">The token cache to target during authentication.</param>
        /// <param name="expiration">The token expiration.</param>
        /// <returns>A ServiceClientCredentials object that can authenticate http requests as the given application.</returns>
        internal static async Task<ServiceClientCredentials> LoginSilentAsync(string domain, string clientId,
            IApplicationAuthenticationProvider authenticationProvider, ActiveDirectoryServiceSettings settings, TokenCache cache, DateTimeOffset expiration)
        {
            var audience = settings.TokenAudience.ToString();
            var context = GetAuthenticationContext(domain, settings, cache);
            var authResult = await authenticationProvider.AuthenticateAsync(clientId, audience, context);
            return new TokenCredentials(new ApplicationTokenProvider(context, audience, clientId,
                    authenticationProvider, authResult, expiration));
        }
#endif
        /// <summary>
        /// Gets an access token from the token cache or from AD authentication endpoint. 
        /// Attempts to refresh the access token if it has expired.
        /// </summary>
        public virtual async Task<AuthenticationHeaderValue> GetAuthenticationHeaderAsync(CancellationToken cancellationToken)
        {
            try
            {
                AuthenticationResult result;
                if (AccessTokenExpired)
                {
                    result = await this._authentications.AuthenticateAsync(this._clientId, this._tokenAudience, this._authenticationContext).ConfigureAwait(false);
                    this._accessToken = result.AccessToken;
                    this._accessTokenType = result.AccessTokenType;
                    this._expiration = result.ExpiresOn;
                }

                return new AuthenticationHeaderValue(this._accessTokenType, this._accessToken);
            }
            catch (AdalException authenticationException)
            {
                throw new AuthenticationException(Resources.ErrorAcquiringToken, authenticationException);
            }
        }

        protected virtual bool AccessTokenExpired
        {
            get { return DateTime.UtcNow + ExpirationThreshold >= this._expiration; }
        }

        private void Initialize(AuthenticationContext context, string tokenAudience, string clientId,
            IApplicationAuthenticationProvider authenticationStore, AuthenticationResult authenticationResult, DateTimeOffset tokenExpiration)
        {
            if (context == null)
            {
                throw new ArgumentNullException("context");
            }

            if (string.IsNullOrWhiteSpace(tokenAudience))
            {
                throw new ArgumentNullException("tokenAudience");
            }

            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new ArgumentNullException("clientId");
            }

            if (authenticationStore == null)
            {
                throw new ArgumentNullException("authenticationStore");
            }
            if (authenticationResult == null)
            {
                throw new ArgumentNullException("authenticationResult");
            }

            this._authentications = authenticationStore;
            this._clientId = clientId;
            this._authenticationContext = context;
            this._accessToken = authenticationResult.AccessToken;
            this._accessTokenType = authenticationResult.AccessTokenType;
            this._tokenAudience = tokenAudience;
            this._expiration = tokenExpiration;
        }

        private static AuthenticationContext GetAuthenticationContext(string domain, ActiveDirectoryServiceSettings serviceSettings, TokenCache cache)
        {
            return (cache == null)
                    ? new AuthenticationContext(serviceSettings.AuthenticationEndpoint + domain,
                        serviceSettings.ValidateAuthority)
                    : new AuthenticationContext(serviceSettings.AuthenticationEndpoint + domain,
                        serviceSettings.ValidateAuthority,
                        cache);
        }
    }
}
