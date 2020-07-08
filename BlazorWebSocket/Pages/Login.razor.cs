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
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Web;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components;

namespace BlazorWebSocket.Pages {

    public class LoginBase : ComponentBase {

        //--- Properties ---
        protected string LoginUrl { get; set; }
        [Inject] private HttpClient HttpClient { get; set; }
        [Inject] private NavigationManager NavigationManager { get; set; }
        [Inject] private CognitoSettings CognitoSettings { get; set; }
        [Inject] private ILocalStorageService LocalStorage { get; set; }

        //--- Methods ---
        protected override async Task OnInitializedAsync() {

            // remove any previous authentication tokens
            await LocalStorage.RemoveItemAsync("Tokens");

            // check if page is loaded with a authorization grant code (i.e. ?code=XYZ)
            var queryParameters = HttpUtility.ParseQueryString(new Uri(NavigationManager.Uri).Query);
            var code = queryParameters["code"];
            if(!string.IsNullOrEmpty(code)) {

                // TODO: use 'state' to protect against replay attacks
                var state = queryParameters["state"];

                // fetch the authorization token from Cognito
                Console.WriteLine($"Fetching authentication tokens for code grant: {code}");
                var oauth2TokenResponse = await HttpClient.PostAsync($"{CognitoSettings.UserPoolUri}/oauth2/token", new FormUrlEncodedContent(new[] {
                    new KeyValuePair<string, string>("grant_type", "authorization_code"),
                    new KeyValuePair<string, string>("code", code),
                    new KeyValuePair<string, string>("client_id", CognitoSettings.ClientId),
                    new KeyValuePair<string, string>("redirect_uri", CognitoSettings.RedirectUri)
                }));
                if(oauth2TokenResponse.IsSuccessStatusCode) {

                    // store authentication tokens in local storage
                    var json = await oauth2TokenResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Storing authentication tokens: {json}");
                    await LocalStorage.SetItemAsync("Tokens", JsonSerializer.Deserialize<AuthenticationTokens>(json));
                }

                // navigate back to main page to connect to the websocket
                NavigationManager.NavigateTo("/");
            } else {
                Console.WriteLine("No code grant to fetch!");
                LoginUrl = CognitoSettings.GetLoginUrl("TBD");
            }
        }
    }
}