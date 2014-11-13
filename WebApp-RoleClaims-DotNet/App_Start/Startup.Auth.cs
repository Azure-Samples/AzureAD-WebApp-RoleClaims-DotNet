﻿using Owin;
using System;
using System.Collections.Generic;

//The following libraries were added to this sample.
using System.Threading.Tasks;
using System.Web;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OpenIdConnect;
using Microsoft.Owin.Security;

//The following libraries were defined and added to this sample.
using WebApp_RoleClaims_DotNet.Utils;
using System.Security.Claims;
using Newtonsoft.Json;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net;
using Microsoft.Azure.ActiveDirectory.GraphClient;
using Microsoft.Azure.ActiveDirectory.GraphClient.Extensions;


namespace WebApp_RoleClaims_DotNet
{
    public partial class Startup
    {
        /// <summary>
        /// Configures OpenIDConnect Authentication & Adds Custom Application Authorization Logic on User Login.
        /// </summary>
        /// <param name="app">The application represented by a <see cref="IAppBuilder"/> object.</param>
        public void ConfigureAuth(IAppBuilder app)
        {
            app.SetDefaultSignInAsAuthenticationType(CookieAuthenticationDefaults.AuthenticationType);

            app.UseCookieAuthentication(new CookieAuthenticationOptions());

            //Configure OpenIDConnect, register callbacks for OpenIDConnect Notifications
            app.UseOpenIdConnectAuthentication(
                new OpenIdConnectAuthenticationOptions
                {
                    ClientId = ConfigHelper.ClientId,
                    //Authority = String.Format(CultureInfo.InvariantCulture, ConfigHelper.AadInstance, ConfigHelper.Tenant), // For Single-Tenant
                    Authority = ConfigHelper.CommonAuthority, // For Multi-Tenant
                    PostLogoutRedirectUri = ConfigHelper.PostLogoutRedirectUri,

                    // Here, we've disabled issuer validation for the multi-tenant sample.  This enables users
                    // from ANY tenant to sign into the application (solely for the purposes of allowing the sample
                    // to be run out-of-the-box.  For a real multi-tenant app, reference the issuer validation in 
                    // WebApp-MultiTenant-OpenIDConnect-DotNet.  If you're running this sample as a single-tenant
                    // app, you can delete the following 4 lines.
                    TokenValidationParameters = new System.IdentityModel.Tokens.TokenValidationParameters // For Multi-Tenant Only
                    {
                        ValidateIssuer = false,
                    },

                    Notifications = new OpenIdConnectAuthenticationNotifications
                    {
                        SecurityTokenValidated = context =>
                        {
                            // Add MVC-Specific Role Claims for each AAD Role Claim Received
                            foreach (Claim claim in claimsId.FindAll("roles"))
                                claimsId.AddClaim(new Claim(ClaimTypes.Role, claim.Value, ClaimValueTypes.String, "WebApp_RoleClaims_DotNet"));
                        },

                        AuthorizationCodeReceived = async context =>
                        {
                            // Set Tenant-Dependent Configuration Values
                            ClaimsIdentity claimsId = context.AuthenticationTicket.Identity; 
                            string tenantId = claimsId.FindFirst(Globals.TenantIdClaimType).Value;
                            ConfigHelper.Authority = String.Format(CultureInfo.InvariantCulture, ConfigHelper.AadInstance, tenantId);
                            ConfigHelper.GraphServiceRoot = new Uri (ConfigHelper.GraphResourceId + tenantId);

                            // Get Access Token for User's Directory
                            try
                            {
                                string userObjectId = claimsId.FindFirst(Globals.ObjectIdClaimType).Value;
                                ClientCredential credential = new ClientCredential(ConfigHelper.ClientId, ConfigHelper.AppKey);
                                AuthenticationContext authContext = new AuthenticationContext(ConfigHelper.Authority, new TokenDbCache(userObjectId));
                                AuthenticationResult result = authContext.AcquireTokenByAuthorizationCode(
                                    context.Code, new Uri(HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Path)), credential, ConfigHelper.GraphResourceId);
                            }
                            catch (AdalException)
                            {
                                context.HandleResponse();
                                context.Response.Redirect("/Error/ShowError?errorMessage=Were having trouble signing you in&signIn=true");
                            }
                        }
                    }
                });
        }
    }
}