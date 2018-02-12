using IdentityModel.Client;
using Microsoft.IdentityModel.Protocols;
using Microsoft.Owin;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.OpenIdConnect;
using Owin;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IdentityModel.Tokens;
using System.Security.Claims;
using System.Threading.Tasks;

[assembly: OwinStartup(typeof(Ajf.MvcTestClient.Mvc.Startup))]

namespace Ajf.MvcTestClient.Mvc
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            JwtSecurityTokenHandler.InboundClaimTypeMap = new Dictionary<string, string>();

            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = "Cookies"
            });

            var BaseAddress = ConfigurationManager.AppSettings["IdentityServerApplicationUrl"];

            string AuthorizeEndpoint = BaseAddress + "/connect/authorize";
            string LogoutEndpoint = BaseAddress + "/connect/endsession";
            string TokenEndpoint = BaseAddress + "/connect/token";
            string UserInfoEndpoint = BaseAddress + "/connect/userinfo";
            string IdentityTokenValidationEndpoint = BaseAddress + "/connect/identitytokenvalidation";
            string TokenRevocationEndpoint = BaseAddress + "/connect/revocation";

            app.UseOpenIdConnectAuthentication(new OpenIdConnectAuthenticationOptions
            {
                ClientId = "mvc.owin.hybrid",
                Authority = BaseAddress,
                RedirectUri = "http://localhost:49314/",
                PostLogoutRedirectUri = "http://localhost:49314/",
                ResponseType = "code id_token token",
                Scope = "openid profile address gallerymanagement roles offline_access email",

                //TokenValidationParameters = new TokenValidationParameters
                //{
                //    NameClaimType = "name",
                //    RoleClaimType = "role"
                //},

                SignInAsAuthenticationType = "Cookies",

                Notifications = new OpenIdConnectAuthenticationNotifications
                {
                    AuthorizationCodeReceived = async n =>
                    {
                            // use the code to get the access and refresh token
                            var tokenClient = new TokenClient(
                            TokenEndpoint,
                            "mvc.owin.hybrid",
                            "secret");

                        var tokenResponse = await tokenClient.RequestAuthorizationCodeAsync(
                            n.Code, n.RedirectUri);

                        if (tokenResponse.IsError)
                        {
                            throw new Exception(tokenResponse.Error);
                        }

                            // use the access token to retrieve claims from userinfo
                            var userInfoClient = new UserInfoClient(
                         UserInfoEndpoint);

                        var userInfoResponse = await userInfoClient.GetAsync(tokenResponse.AccessToken);

                            // create new identity
                            var id = new ClaimsIdentity(n.AuthenticationTicket.Identity.AuthenticationType);
                        id.AddClaims(userInfoResponse.Claims);

                        id.AddClaim(new Claim("access_token", tokenResponse.AccessToken));
                        id.AddClaim(new Claim("expires_at", DateTime.Now.AddSeconds(tokenResponse.ExpiresIn).ToLocalTime().ToString()));
                        id.AddClaim(new Claim("id_token", n.ProtocolMessage.IdToken));
                        id.AddClaim(new Claim("sid", n.AuthenticationTicket.Identity.FindFirst("sid").Value));

                        n.AuthenticationTicket = new AuthenticationTicket(
                            new ClaimsIdentity(id.Claims, n.AuthenticationTicket.Identity.AuthenticationType, "name", "role"),
                            n.AuthenticationTicket.Properties);
                    },

                    RedirectToIdentityProvider = n =>
                    {
                            // if signing out, add the id_token_hint
                            if (n.ProtocolMessage.RequestType == OpenIdConnectRequestType.LogoutRequest)
                        {
                            var idTokenHint = n.OwinContext.Authentication.User.FindFirst("id_token");

                            if (idTokenHint != null)
                            {
                                n.ProtocolMessage.IdTokenHint = idTokenHint.Value;
                            }

                        }

                        return Task.FromResult(0);
                    }
                }
            });
        }
    }
}
