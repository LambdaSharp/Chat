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
using Blazored.LocalStorage;
using Demo.WebSocketsChat.Common.Notifications;
using Demo.WebSocketsChat.Common.Requests;
using Microsoft.AspNetCore.Components;

namespace BlazorWebSocket.Pages {

    public class IndexBase : ComponentBase, IDisposable {

        //--- Constants ---
        private const int TOKEN_EXPIRATION_LIMIT_SECONDS = 5 * 60;

        //--- Types ---
        protected enum ConnectionState {
            Initializing,
            Unauthorized,
            Connecting,
            Connected
        }

        //--- Fields ---

        //--- Properties ---
        protected List<UserMessageChangedNotification> Messages { get; set; } = new List<UserMessageChangedNotification>();
        protected string ChatMessage { get; set; }
        protected string UserId { get; set; }
        protected string UserName { get; set; }
        protected ConnectionState State { get; set; } = ConnectionState.Initializing;
        protected WebSocketDispatch WebSocketDispatch { get; set; }
        protected string LoginUrl;
        [Inject] private HttpClient HttpClient { get; set; }
        [Inject] private ILocalStorageService LocalStorage { get; set; }
        [Inject] private CognitoSettings CognitoSettings { get; set; }

        //--- Methods ---
        protected override async Task OnInitializedAsync() {

            // configure WebSocket
            WebSocketDispatch = new WebSocketDispatch(new Uri($"wss://{HttpClient.BaseAddress.Host}/socket"));
            WebSocketDispatch.RegisterAction<UserMessageChangedNotification>("message", ReceivedMessage);
            WebSocketDispatch.RegisterAction<UserNameChangedNotification>("username", ReceivedUserNameChanged);
            WebSocketDispatch.RegisterAction<WelcomeNotification>("welcome", ReceivedWelcomeAsync);
            WebSocketDispatch.RegisterAction<JoinedChannelNotification>("joined", ReceivedJoinedChannel);

            // attempt to restore authentication tokens from local storage
            var authenticationTokens = await GetAuthenticationTokens();
            if(authenticationTokens == null) {
                Console.WriteLine("No authentication tokens found");

                // TODO: create 'state' to protect against replay attacks
                LoginUrl = CognitoSettings.GetLoginUrl(state: "TBD");
                Console.WriteLine($"Login URL: {LoginUrl}");
                State = ConnectionState.Unauthorized;
            } else {
                Console.WriteLine("Attempting to connect to websocket");
                State = ConnectionState.Connecting;

                // attempt to connect to the websocket
                WebSocketDispatch.IdToken = authenticationTokens.IdToken;
                await WebSocketDispatch.Connect();

                // TODO: set timer to refresh tokens and reconnect websocket
            }
        }

        protected async Task SendMessageAsync() {
            await WebSocketDispatch.SendMessageAsync(new SendMessageRequest {
                ChannelId = "General",
                Text = ChatMessage
            });
            ChatMessage = "";
        }

        protected async Task RenameUserAsync() {
            await WebSocketDispatch.SendMessageAsync(new RenameUserRequest {
                UserName = UserName
            });
        }

        private void ReceivedMessage(UserMessageChangedNotification message) {
            Console.WriteLine($"Received UserMessage: UserId={message.UserId}, UserName={message.UserName}, ChannelId={message.ChannelId}, Text='{message.Text}', Timestamp={message.Timestamp}");

            // add message to message list
            Messages.Add(message);

            // update user interface
            StateHasChanged();
        }

        private void ReceivedUserNameChanged(UserNameChangedNotification username) {
            Console.WriteLine($"Received UserNameChanged: UserId={username.UserId}, UserName={username.UserName}, OldUserName={username.OldUserName}");

            // check if user name change notification is about us or somebody else
            if(username.UserId == UserId) {

                // update our user name
                UserName = username.UserName;
            }

            // show message about renamed user
            Messages.Add(new UserMessageChangedNotification {
                UserId = "#host",
                Text = $"{username.OldUserName} is now known as {username.UserName}",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            // update user interface
            StateHasChanged();
        }

        private async Task ReceivedWelcomeAsync(WelcomeNotification welcome) {
            Console.WriteLine($"Received Welcome: UserId={welcome.UserId}, UserName={welcome.UserName}");

            // update application state
            UserId = welcome.UserId;
            UserName = welcome.UserName;
            State = ConnectionState.Connected;

            // update user interface
            StateHasChanged();
        }

        private void ReceivedJoinedChannel(JoinedChannelNotification joined) {
            Console.WriteLine($"Received JoinedChannel: UserId={joined.UserId}, UserName={joined.UserName}, ChannelId={joined.ChannelId}");

            // show message about renamed user
            Messages.Add(new UserMessageChangedNotification {
                UserId = "#host",
                Text = $"{joined.UserName} has joined",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });

            // update user interface
            StateHasChanged();
        }

        private async Task<AuthenticationTokens> GetAuthenticationTokens() {

            // check if any authentication tokens are stored
            var authenticationTokens = await LocalStorage.GetItemAsync<AuthenticationTokens>("Tokens");
            if(authenticationTokens == null) {
                return null;
            }

            // check if tokens will expire in 5 minutes or less
            if(DateTimeOffset.FromUnixTimeSeconds(authenticationTokens.Expiration) >= DateTimeOffset.UtcNow.AddSeconds(-TOKEN_EXPIRATION_LIMIT_SECONDS)) {

                // refresh authentication tokens
                Console.WriteLine($"Refreshgin authentication tokens for code grant: {authenticationTokens.IdToken}");
                var oauth2TokenResponse = await HttpClient.PostAsync($"{CognitoSettings.UserPoolUri}/oauth2/token", new FormUrlEncodedContent(new[] {
                    new KeyValuePair<string, string>("grant_type", "refresh_token"),
                    new KeyValuePair<string, string>("refresh_token ", authenticationTokens.RefreshToken),
                    new KeyValuePair<string, string>("client_id", CognitoSettings.ClientId)
                }));
                if(!oauth2TokenResponse.IsSuccessStatusCode) {

                    // remove previous authentication tokens
                    await LocalStorage.RemoveItemAsync("Tokens");
                    return null;
                }

                // store authentication tokens in local storage
                var json = await oauth2TokenResponse.Content.ReadAsStringAsync();
                Console.WriteLine($"Storing authentication tokens: {json}");
                authenticationTokens = JsonSerializer.Deserialize<AuthenticationTokens>(json);
                await LocalStorage.SetItemAsync("Tokens", authenticationTokens);
            }
            return authenticationTokens;
        }

        //--- IDisposable Members ---
        void IDisposable.Dispose() => WebSocketDispatch.Dispose();
    }
}