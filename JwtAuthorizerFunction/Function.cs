/*
 * LambdaSharp (λ#)
 * Copyright (C) 2018-2020
 * lambdasharp.net
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;

namespace LambdaSharp.Chat.JwtAuthorizerFunction {

    public sealed class Function : ALambdaFunction<AuthorizationRequest, AuthorizationResponse> {

        //--- Types ---
        public class Header {

            //--- Properties ---
            public string Authorization { get; set; }
        }

        //--- Fields ---
        private string _issuer;
        private string _audience;
        private bool _enabled;
        private JsonWebKeySet _issuerJsonWebKeySet;

        //--- Constructors ---
        public Function() : base(new LambdaSharp.Serialization.LambdaSystemTextJsonSerializer()) { }

        //--- Methods ---
        public override async Task InitializeAsync(LambdaConfig config) {

            // read configuration settings
            _issuer = config.ReadText("Issuer");
            _audience = config.ReadText("Audience");
            _enabled = config.ReadText("Enabled", "true").Equals("true", StringComparison.OrdinalIgnoreCase);

            // fetch JsonWebKeySet from issuer
            var jsonWebKeySetUrl = _issuer.EndsWith("/")
                ? _issuer + ".well-known/jwks.json"
                : _issuer + "/.well-known/jwks.json";
            var response = await HttpClient.GetAsync(jsonWebKeySetUrl);
            _issuerJsonWebKeySet = new JsonWebKeySet(await response.Content.ReadAsStringAsync());
        }

        public override async Task<AuthorizationResponse> ProcessMessageAsync(AuthorizationRequest request) {
            LogInfo($"Validation enabled: {_enabled}");

            // read Authorization token
            var authorization = GetAuthorizationToken();
            Dictionary<string, string> claims = null;
            if(string.IsNullOrEmpty(authorization)) {
                LogInfo("Unauthorized: missing Authorization token");
            } else {
                try {

                    // parse JWT without validation
                    LogInfo($"Parsing JWT: \"{authorization}\"");
                    claims = new JwtSecurityTokenHandler()
                        .ReadJwtToken(authorization)
                        .Claims
                        .ToDictionary(claim => claim.Type, claim => claim.Value);
                    LogInfo($"JWT Claims: {JsonSerializer.Serialize(claims)}");

                    // validate JWT value
                    LogInfo($"Validating JWT");
                    try {
                        new JwtSecurityTokenHandler().ValidateToken(authorization, new TokenValidationParameters {
                            IssuerSigningKeys = _issuerJsonWebKeySet.Keys,
                            ValidIssuer = _issuer,
                            ValidAudience = _audience
                        }, out var _).Claims.ToDictionary(claim => $"jwt:{claim.Type}", claim => claim.Value);
                        return CreateResponse(success: true);
                    } catch(SecurityTokenExpiredException) {
                        LogInfo("Unauthorized: token expired");
                    } catch(SecurityTokenInvalidAlgorithmException) {
                        LogInfo("Unauthorized: invalid algorithm");
                    } catch(SecurityTokenInvalidAudienceException) {
                        LogInfo("Unauthorized: invalid audience");
                    } catch(SecurityTokenInvalidIssuerException) {
                        LogInfo("Unauthorized: invalid issuer");
                    } catch(SecurityTokenInvalidLifetimeException) {
                        LogInfo("Unauthorized: invalid lifetime");
                    } catch(SecurityTokenInvalidSignatureException) {
                        LogInfo("Unauthorized: invalid signature");
                    } catch(SecurityTokenInvalidSigningKeyException) {
                        LogInfo("Unauthorized: invalid signing keys");
                    } catch(SecurityTokenInvalidTypeException) {
                        LogInfo("Unauthorized: invalid type");
                    } catch(SecurityTokenNoExpirationException) {
                        LogInfo("Unauthorized: no expiration");
                    } catch(SecurityTokenNotYetValidException) {
                        LogInfo("Unauthorized: not yet valid");
                    } catch(SecurityTokenReplayAddFailedException) {
                        LogInfo("Unauthorized: replay add failed");
                    } catch(SecurityTokenReplayDetectedException) {
                        LogInfo("Unauthorized: replay detected");
                    } catch(SecurityTokenValidationException) {
                        LogInfo("Unauthorized: validation failed");
                    }
                } catch(Exception e) {
                    LogErrorAsInfo(e, "error parsing JWT");
                }
            }
            return CreateResponse(success: false);

            // local function
            string GetAuthorizationToken() {
                const string AuthorizationHeaderPrefix = "Bearer ";

                // check if 'header' query parameter is set
                string encodedHeader = null;
                if(request.QueryStringParameters?.TryGetValue("header", out encodedHeader) ?? false) {
                    try {
                        var header = System.Text.Json.JsonSerializer.Deserialize<Header>(Encoding.UTF8.GetString(Convert.FromBase64String(encodedHeader)));
                        if(header.Authorization != null) {
                            return header.Authorization.StartsWith(AuthorizationHeaderPrefix)
                                ? header.Authorization.Substring(AuthorizationHeaderPrefix.Length).Trim()
                                : null;
                        }
                    } catch(Exception e) {
                        LogErrorAsInfo(e, "unable to decode header query parameter");
                    }
                }

                // convert headers to be case-insensitive
                var headers = new Dictionary<string, string>(request.Headers, StringComparer.InvariantCultureIgnoreCase);

                // check if 'Authorization' header is set
                if(headers.TryGetValue("Authorization", out var authorizationHeader)) {
                    if(authorizationHeader.StartsWith(AuthorizationHeaderPrefix)) {
                        return authorizationHeader.Substring(AuthorizationHeaderPrefix.Length).Trim();
                    }

                    // not a valid 'Authorization' header value
                    return null;
                }
                return null;
            }

            AuthorizationResponse CreateResponse(bool success) {

                // use claims subject as principal if available
                string principal = null;
                claims?.TryGetValue("sub", out principal);

                // create response with claims as context
                return new AuthorizationResponse {
                    PrincipalId = principal ?? "user",
                    PolicyDocument = new PolicyDocument {
                        Statement = {
                            new Statement {
                                Sid = "JwtAuthorization",
                                Action = "execute-api:Invoke",
                                Effect = (success || !_enabled)
                                    ? "Allow"
                                    : "Deny",
                                Resource = request.MethodArn
                            }
                        }
                    },
                    Context = claims
                };
            }
        }
    }
}
