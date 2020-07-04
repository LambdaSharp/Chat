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
using System.Threading.Tasks;
using LambdaSharp;
using Microsoft.IdentityModel.Tokens;

namespace Demo.WebSocketsChat.JwtAuthorizerFunction {

    public sealed class Function : ALambdaFunction<AuthorizationRequest, AuthorizationResponse> {

        //--- Fields ---
        private string _issuer;
        private string _audience;
        private bool _enabled;
        private JsonWebKeySet _issuerJsonWebKeySet;

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
            var claims = new Dictionary<string, string>();
            if(string.IsNullOrEmpty(authorization)) {
                Fail("Unauthorized: missing Authorization token");
            } else {
                LogInfo($"Validating JWT: \"{authorization}\"");

                // validate JWT value
                try {
                    claims = new JwtSecurityTokenHandler().ValidateToken(authorization, new TokenValidationParameters {
                        IssuerSigningKeys = _issuerJsonWebKeySet.Keys,
                        ValidIssuer = _issuer,
                        ValidAudience = _audience
                    }, out var _).Claims.ToDictionary(claim => $"jwt:{claim.Type}", claim => claim.Value);
                } catch(SecurityTokenExpiredException) {
                    Fail("Unauthorized: token expired");
                } catch(SecurityTokenInvalidAlgorithmException) {
                    Fail("Unauthorized: invalid algorithm");
                } catch(SecurityTokenInvalidAudienceException) {
                    Fail("Unauthorized: invalid audience");
                } catch(SecurityTokenInvalidIssuerException) {
                    Fail("Unauthorized: invalid issuer");
                } catch(SecurityTokenInvalidLifetimeException) {
                    Fail("Unauthorized: invalid lifetime");
                } catch(SecurityTokenInvalidSignatureException) {
                    Fail("Unauthorized: invalid signature");
                } catch(SecurityTokenInvalidSigningKeyException) {
                    Fail("Unauthorized: invalid signing keys");
                } catch(SecurityTokenInvalidTypeException) {
                    Fail("Unauthorized: invalid type");
                } catch(SecurityTokenNoExpirationException) {
                    Fail("Unauthorized: no expiration");
                } catch(SecurityTokenNotYetValidException) {
                    Fail("Unauthorized: not yet valid");
                } catch(SecurityTokenReplayAddFailedException) {
                    Fail("Unauthorized: replay add failed");
                } catch(SecurityTokenReplayDetectedException) {
                    Fail("Unauthorized: replay detected");
                } catch(SecurityTokenValidationException) {
                    Fail("Unauthorized: validation failed");
                }
            }

            // authorize user to continue
            claims.TryGetValue("sub", out var principal);
            return new AuthorizationResponse {
                PrincipalId = principal ?? "user",
                PolicyDocument = new PolicyDocument {
                    Statement = {
                        new Statement {
                            Sid = "JwtAuthorization",
                            Action = "execute-api:Invoke",
                            Effect = "Allow",
                            Resource = request.MethodArn
                        }
                    }
                },
                Context = claims
            };

            // local function
            string GetAuthorizationToken() {
                const string AuthorizationHeaderPrefix = "Bearer ";

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

                // check if 'id_token' query parameter is set
                string authorizationParameter = null;
                if(request.QueryStringParameters?.TryGetValue("id_token", out authorizationParameter) ?? false) {
                    return authorizationParameter;
                }
                return null;
            }

            void Fail(string reason) {
                LogInfo(reason);
                if(_enabled) {
                    throw new Exception("Unauthorized");
                }

                // proceed despite failure; attempt to parse JWT without validation
                if(!string.IsNullOrEmpty(authorization)) {
                    LogInfo($"Parsing JWT: \"{authorization}\"");
                    try {
                        claims = new JwtSecurityTokenHandler()
                            .ReadJwtToken(authorization)
                            .Claims
                            .ToDictionary(claim => claim.Type, claim => claim.Value);
                    } catch(Exception e) {
                        LogErrorAsInfo(e, "error pasing JWT");
                    }
                }
            }
        }
    }
}
