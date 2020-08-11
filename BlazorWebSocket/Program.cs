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
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Blazored.LocalStorage;
using BlazorWebSocket.Common;
using LambdaSharp.App;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BlazorWebSocket {

    public class Program {

        //--- Class Methods ---
        public static async Task Main(string[] args) {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("app");

            // initialize LambdaSharp dependencies
            builder.AddLambdaSharp<Program>();

            // register cognito configuration
            builder.Services.AddSingleton(sp => {
                var config = new CognitoSettings();
                builder.Configuration.GetSection("Cognito").Bind(config);
                return config;
            });

            // enable access to browser local storage
            builder.Services.AddBlazoredLocalStorage();
            await builder.Build().RunAsync();
        }
    }
}
