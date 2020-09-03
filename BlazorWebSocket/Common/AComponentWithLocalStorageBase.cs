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
using System.Threading.Tasks;
using Blazored.LocalStorage;
using LambdaSharp.App;
using Microsoft.AspNetCore.Components;

namespace BlazorWebSocket.Common {

    public abstract class AComponentWithLocalStorageBase : ALambdaComponent {

        //--- Properties ---
        [Inject] protected ILocalStorageService LocalStorage { get; set; }

        //--- Methods ---
        protected Task SaveAsync<T>(string name, T item) => LocalStorage.SetItemAsync(name, item);
        protected Task<T> LoadAsync<T>(string name) => LocalStorage.GetItemAsync<T>(name);
        protected Task SaveTokensAsync(AuthenticationTokens tokens) => SaveAsync("Tokens", tokens);
        protected Task<AuthenticationTokens> LoadTokensAsync() => LoadAsync<AuthenticationTokens>("Tokens");
        protected Task ClearTokensAsync() => LocalStorage.RemoveItemAsync("Tokens");

        protected async Task<string> CreateReplayGuardAsync() {
            var state = new Guid().ToString();
            await SaveAsync("AuthenticationGuard", state);
            return state;
        }

        protected async Task<bool> VerifyReplayGuardAsync(string receivedGuard) {
            var storedGuard = await LoadAsync<string>("AuthenticationGuard");
            await LocalStorage.RemoveItemAsync("AuthenticationGuard");
            return storedGuard == receivedGuard;
        }
    }
}
