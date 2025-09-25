var PluginConfig = {
    pluginUniqueId: 'c78f11cf-93e6-4423-8c42-d2c255b70e47'
};

export default function (view, params) {
    view.addEventListener('viewshow', async function () {
        var generalFunctionsUrl = ApiClient.getUrl("web/ConfigurationPage", { name: "AniSync_CommonJs" });
        import(generalFunctionsUrl).then(async (generalFunctions) => await initialLoad(generalFunctions))
    });
}

async function initialLoad(common) {
    const page = document;
    common.setTabs(common.TabGeneral, common.getTabs);
    Dashboard.showLoadingMsg();

    ApiClient.getUsers()
        .then(function (users) {
            common.populateUserList(page, users, '#selectUser');
            loadUserConfiguration(page.querySelector('#selectUser').value);
            setUserAddress(page);
        })
        .catch(error => console.log("Could not populate users list: " + error));

    await setParameters(common, page);
    loadProviderConfiguration(page);
    Dashboard.hideLoadingMsg();


    page.querySelector('#selectUser')
        .addEventListener('change', function () {
            loadUserConfiguration(page.querySelector('#selectUser').value);
            page.querySelector('#authorizeLink').innerHTML = '';
        });

    page.querySelector('#selectProvider')
        .addEventListener('change', function () {
            loadProviderConfiguration(page);
        });

    page.querySelector('#TemplateConfigForm')
        .addEventListener('submit', function (e) {
            saveUserConfig(common);
            e.preventDefault();
            return false;
        });

    page.querySelector('#testAnimeListSaveLocation').onclick = await runTestAnimeListSaveLocation;
    page.querySelector('#generateCallbackUrlButton').onclick = generateCallbackUrl;
    page.querySelector('#authorizeDevice').onclick = (async () => await onAuthorizeButtonClick(common));
    page.querySelector('#testAuthentication').onclick = (() => getUser(common));

    async function runTestAnimeListSaveLocation() {
        document.querySelector('#testAnimeListSaveLocationResponse').innerHTML = "Testing anime list save location..."
        var location = document.querySelector('#animeListSaveLocation').value;
        if (!location) {
            document.querySelector('#testAnimeListSaveLocationResponse').innerHTML = "Error: Save location is empty";
            return;
        }

        var url = ApiClient.getUrl("/AniSync/testAnimeListSaveLocation?saveLocation=" + encodeURIComponent(location));
        await ApiClient.ajax({
            type: 'GET',
            url
        })
            .then((response) => response.json())
            .then((result) => {
                if (result === "") {
                    document.querySelector('#testAnimeListSaveLocationResponse').innerHTML = "Anime list save location is valid! Please remember to save."
                } else {
                    document.querySelector('#testAnimeListSaveLocationResponse').innerHTML = "Error: " + result;
                }
            })
            .catch((error) => {
                Promise.resolve(error).then(async (resolvedError) => {
                    if (typeof (resolvedError) === "string") {
                        return resolvedError;
                    } else {
                        await resolvedError.text().then(error => {
                            document.querySelector('#testAnimeListSaveLocationResponse').innerHTML = "Error: " +  error;
                        });
                    }
                });

                // reset save file path so the user doesn't accidentally save an invalid path
                ApiClient.getPluginConfiguration(PluginConfig.pluginUniqueId).then(function (config) {
                     document.querySelector('#animeListSaveLocation').value = config.animeListSaveLocation ?? '';
                });
            })
    }


    function generateCallbackUrl() {
        const userApiUrl = document.querySelector('#apiUrl').value;
        if (userApiUrl) {
            document.querySelector('#generalCallbackUrlInput').value = userApiUrl + "/AniSync/authCallback"
        } else {
            document.querySelector('#generalCallbackUrlInput').value = PluginConfig.localApiUrl + "/AniSync/authCallback"
        }
    }


    async function onAuthorizeButtonClick(common) {
        document.querySelector('#authorizeClientIdError').innerHTML = "";
        document.querySelector('#authorizeClientSecretError').innerHTML = "";

        var kitsuAuth = document.querySelector('#selectProvider').value === "Kitsu";
        var clientId = document.querySelector('#clientId').value;
        var clientSecret = document.querySelector('#clientSecret').value;
        if (!clientId || !clientSecret) {
            if (!clientId)
                document.querySelector('#authorizeClientIdError').innerHTML = `Error: ${kitsuAuth ? "Username" : "Client ID"} is empty.`;

            if (!clientSecret)
                document.querySelector('#authorizeClientSecretError').innerHTML = `Error: ${kitsuAuth ? "Username" : "Client Secret"} is empty.`;

            return
        }

        // users are unlikely to save after setting client id and secret, so we do it for them
        saveUserConfig(common, true);
        if (kitsuAuth) {
            var url = ApiClient.getUrl("/AniSync/passwordGrant?provider=Kitsu&userId=" + encodeURIComponent(document.querySelector('#selectUser').value) +
                "&username=" + encodeURIComponent(clientId) +
                "&password=" + encodeURIComponent(clientSecret));
            await ApiClient.ajax({
                type: "GET",
                url
            })
                .then((_) => document.querySelector('#authorizeLinkGenerationNotification').innerHTML = "Authentication successful.")
                .catch((error) => error.text().then(errorText => document.querySelector('#authorizeLinkGenerationNotification').innerHTML = "Error: " + errorText));
        } else {
            var url = ApiClient.getUrl("/AniSync/buildAuthorizeRequestUrl?provider=" + document.querySelector('#selectProvider').value + "&clientId=" + encodeURIComponent(clientId) +
                "&clientSecret=" + encodeURIComponent(clientSecret) +
                "&url=" + encodeURIComponent((document.querySelector('#apiUrl').value ? document.querySelector('#apiUrl').value : "local")) + 
                "&user=" + document.querySelector('#selectUser').value);
            await ApiClient.ajax({
                type: "GET",
                url
            })
                .then((response) => response.json())
                .catch((_) => document.querySelector('#authorizeLinkGenerationNotification').innerHTML = "Error: Could not generate authorize link. Check the logs for more information.")
                .then((json) => document.querySelector('#authorizeLink').innerHTML = json);
        }
    }

    async function getUser(common) {
        document.querySelector('#getUserResponse').innerHTML = "Testing authentication.. this can take some time."
        if (document.querySelector('#selectProvider').value === "Annict")
            saveUserConfig(common, false);
        var url = ApiClient.getUrl("/AniSync/user?apiName=" + document.querySelector('#selectProvider').value +
            "&userId=" + encodeURIComponent(document.querySelector('#selectUser').value));
        await ApiClient.ajax({
            type: "GET",
            url
        })
            .then(function (response) {
                if (response.ok) {
                    return response.json()
                        .then(function (json) {
                            var userResponseElement = document.querySelector('#getUserResponse');
                            if (json.name) {
                                return userResponseElement.innerHTML = "Thank you for authenticating " + json.name + ".";
                            } else {
                                return userResponseElement.innerHTML = "Thank you for authenticating."
                            }
                        });
                } else {
                    document.querySelector('#getUserResponse').innerHTML = "Test returned an error - try authenticating again or check the logs for a detailed error reason."
                }
            }).catch(function (error) {
                error.text().then(errorText => document.querySelector('#getUserResponse').innerHTML = "Test returned an error: " + errorText + "; try authenticating again or check the logs for a detailed error reason.")
            });
    }

    async function setParameters(common, page) {
        var url = ApiClient.getUrl("/AniSync/parameters");
        await ApiClient.ajax({type: 'GET', url})
            .then(function (response) {
                if (response.ok) {
                    return response.json()
                        .then(function (json) {
                            setLocalApiUrl(page, json.https, json.localIpAddress, json.localPort);
                            common.setProviderSelection(page, json.providerList, '#selectProvider');
                            setCallbackRedirectUrlInputDescription(json.localIpAddress, json.localPort);
                        });
                } else {
                    page.querySelector('#localApiUrl').innerHTML = "Could not fetch local URL.";
                }
            });
    }

    function setLocalApiUrl(page, https, localIpAddress, localPort) {
        var localApiUrl = (https ? "https://" : "http://") + localIpAddress + ":" + localPort;
        PluginConfig.localApiUrl = localApiUrl;
        page.querySelector('#localApiUrl').innerHTML = "Local (server) URL: <b>" + localApiUrl + "</b>";
    }

    function setCallbackRedirectUrlInputDescription(localIpAddress, localPort) {
        page.querySelector("#callbackRedirectUrlDescription").innerHTML = "Redirect the user to this URL on successful authentication.<br></br>Variables: \"{{LocalIpAddress}}\" will be converted to the detected local IP address (" + localIpAddress + "), \"{{LocalPort}}\" will be converted to the detected Jellyfin port (" + localPort + ")."
    }

    function setUserAddress(page) {
        page.querySelector('#userAddress').innerHTML = "User URL: <b>" + ApiClient.serverAddress() + "</b>";
    }

    function loadUserConfiguration(userId) {
        ApiClient.getPluginConfiguration(PluginConfig.pluginUniqueId).then(function (config) {
            let currentUser;
            if (config.UserConfig != null && config.UserConfig.length > 0) {
                currentUser = config.UserConfig.filter(function (item) {
                    return item.UserId === userId;
                })[0];
            } else {
                currentUser = null;
            }

            if (!currentUser) {
                // user does not have an existing configuration setup so use default values.
                currentUser = {};
                currentUser.LibraryToCheck = [];
                currentUser.PlanToWatchOnly = true;
                currentUser.RewatchCompleted = true;
            }

            PluginConfig.LibraryToCheck = currentUser.LibraryToCheck || [];
            document.querySelector('#PlanToWatchOnly').checked = currentUser.PlanToWatchOnly;
            document.querySelector('#RewatchCompleted').checked = currentUser.RewatchCompleted;
            Dashboard.hideLoadingMsg();

            ApiClient.getVirtualFolders(PluginConfig.pluginUniqueId).then(function (result) {
                var html = '';
                html += '<div data-role="controlgroup">';
                for (var x = 0; x < result.length; x++) {
                    html += '<label><input ';
                    if (PluginConfig.LibraryToCheck.includes(result[x].ItemId)) {
                        html += 'checked="true" ';
                    }
                    html += 'is="emby-checkbox" class="library" type="checkbox" data-mini="true" id="' + result[x].ItemId + '" name="' + result[x].Name + '"/><span>' + result[x].Name + '</span></label>';
                }
                html += '</div>';
                document.querySelector('#libraries').innerHTML = html;
            });
        });
    }

    function loadProviderConfiguration(page) {
        const providerName = page.querySelector('#selectProvider').value;
        Dashboard.showLoadingMsg();
        ApiClient.getPluginConfiguration(PluginConfig.pluginUniqueId).then(function (config) {
            let provider;
            if (config.ProviderApiAuth != null && config.ProviderApiAuth.length > 0) {
                provider = config.ProviderApiAuth.filter(function (item) {
                    return item.Name === providerName;
                })[0];
            } else {
                provider = null;
            }

            if (!provider) {
                provider = {};
                provider.Name = providerName;
                provider.ClientId = "";
                provider.ClientSecret = "";
            }

            page.querySelector('#clientId').value = provider.ClientId;
            page.querySelector('#clientSecret').value = provider.ClientSecret;
            if (config.animeListSaveLocation)
                page.querySelector('#animeListSaveLocation').value = config.animeListSaveLocation;
            if (config.watchedTickboxUpdatesProvider)
                page.querySelector('#watchedTickboxUpdatesProvider').checked = config.watchedTickboxUpdatesProvider;
            if (config.callbackRedirectUrl)
                page.querySelector('#callbackRedirectUrlInput').value = config.callbackRedirectUrl;
            if (config.shikimoriAppName)
                page.querySelector('#shikimoriAppName').value = config.shikimoriAppName;
            if (config.simklUpdateAll)
                page.querySelector('#simklUpdateAll').checked = config.simklUpdateAll;
            if (config.updateNsfw)
                page.querySelector('#UpdateNsfw').checked = config.updateNsfw;
            page.querySelector('#linkTimeExpire').value = config.authenticationLinkExpireTimeMinutes && config.authenticationLinkExpireTimeMinutes !== 0 ? config.authenticationLinkExpireTimeMinutes : 1440;

            page.querySelector('#clientSecretLabel').style.display = "block";
            page.querySelector('#clientSecret').style.display = "block";
            page.querySelector('#clientSecretDescription').style.display = "block";
            page.querySelector('#authorizeDevice').style.display = "block";
            page.querySelector('#authorizeDeviceDescription').style.display = "block";
            page.querySelector('#shikimoriAppNameContainer').style.display = "none";
            page.querySelector('#simklUpdateAllContainer').style.display = "none";
            page.querySelector('#testAuthenticationDescription').innerHTML = "Once you have authenticated your user, click the below button to test the authentication:";
            page.querySelector('#clientIdLabel').innerHTML = "Client ID";
            page.querySelector('#clientIdDescription').innerHTML = "The client ID from your provider application.";
            page.querySelector('#clientSecretLabel').innerHTML = "Client Secret";
            page.querySelector('#clientSecretDescription').innerHTML = "The client secret from your provider application.<b>This value will be stored in plain text in the plugin config. Make sure no untrusted users have access to the file.</b>";
            if (providerName === "Kitsu") {
                page.querySelector('#clientIdLabel').innerHTML = "Username";
                page.querySelector('#clientIdDescription').innerHTML = "The username used to login to the provider application.";
                page.querySelector('#clientSecretLabel').innerHTML = "Password";
                page.querySelector('#clientSecretDescription').innerHTML = "The password used to login to the provider application.<b>This value will be stored in plain text in the plugin config. Make sure no untrusted users have access to the file.</b>";
            } else if (providerName === "Annict") {
                page.querySelector('#clientIdLabel').innerHTML = "Personal Access Token";
                page.querySelector('#clientIdDescription').innerHTML = "The personal access token from your provider application.<b>This value will be stored in plain text in the plugin config. Make sure no untrusted users have access to the file.</b>"
                page.querySelector('#clientSecret').style.display = "none";
                page.querySelector('#clientSecretLabel').style.display = "none";
                page.querySelector('#clientSecretDescription').style.display = "none";
                page.querySelector('#authorizeDevice').style.display = "none";
                page.querySelector('#authorizeDeviceDescription').style.display = "none";
                page.querySelector('#testAuthenticationDescription').innerHTML = "Click the below button to test the authentication:";
            } else if (providerName === "Shikimori") {
                page.querySelector('#shikimoriAppNameContainer').style.display = "block";
            } else if (providerName === "Simkl") {
                page.querySelector('#simklUpdateAllContainer').style.display = "block";
            }

            if (config.callbackUrl)
                page.querySelector('#apiUrl').value = config.callbackUrl;
            Dashboard.hideLoadingMsg();
        });
    }

    function setProviderApiAuthConfig(config) {
        const name = document.querySelector('#selectProvider').value;
        const clientId = document.querySelector('#clientId').value;
        const clientSecret = document.querySelector('#clientSecret').value;
        const apiUrl = document.querySelector('#apiUrl').value;
        if (config.ProviderApiAuth != null && config.ProviderApiAuth.length > 0) {
            let authConfig = config.ProviderApiAuth.filter(function (item) {
                return item.Name === name
            })[0];

            if (!authConfig && clientId && clientSecret) {
                authConfig = {
                    Name: name,
                    ClientId: clientId,
                    clientSecret: clientSecret
                };

                config.ProviderApiAuth.push(authConfig);
            }

            if (clientId && clientSecret && name !== "Kitsu") {
                authConfig.ClientId = clientId;
                authConfig.ClientSecret = clientSecret;
            } else {
                config.ProviderApiAuth.splice(config.ProviderApiAuth.indexOf(authConfig), 1);
            }
        } else {
            config.ProviderApiAuth = [];
            config.ProviderApiAuth.push({
                Name: name,
                ClientId: document.querySelector('#clientId').value,
                ClientSecret: document.querySelector('#clientSecret').value
            });
        }

        if (apiUrl) {
            config.callbackUrl = apiUrl;
        } else {
            delete config.callbackUrl;
        }
    }

    function saveUserConfig(common) {
        ApiClient.getPluginConfiguration(PluginConfig.pluginUniqueId).then(function (config) {
            var userId = document.querySelector('#selectUser').value;

            let userConfig;
            if (config.UserConfig != null && config.UserConfig.length > 0) {
                userConfig = config.UserConfig.filter(function (item) {
                    return item.UserId == userId;
                })[0];
            } else {
                config.UserConfig = [];
                userConfig = null;
            }

            if (!userConfig) {
                userConfig = {};
                config.UserConfig.push(userConfig);
            }
            setProviderApiAuthConfig(config);
            config.animeListSaveLocation = document.querySelector('#animeListSaveLocation').value;
            config.watchedTickboxUpdatesProvider = document.querySelector('#watchedTickboxUpdatesProvider').checked;
            config.callbackRedirectUrl = document.querySelector('#callbackRedirectUrlInput').value;
            config.shikimoriAppName = document.querySelector('#shikimoriAppName').value;
            config.simklUpdateAll = document.querySelector('#simklUpdateAll').checked;
            config.updateNsfw = document.querySelector('#UpdateNsfw').checked;
            config.authenticationLinkExpireTimeMinutes = document.querySelector('#linkTimeExpire').value;

            userConfig.LibraryToCheck = Array.prototype.map.call(document.querySelectorAll('.library:checked'), element => {
                return element.getAttribute('id');
            });
            userConfig.UserId = userId;
            userConfig.PlanToWatchOnly = document.querySelector('#PlanToWatchOnly').checked;
            userConfig.RewatchCompleted = document.querySelector('#RewatchCompleted').checked;

            if (document.querySelector('#selectProvider').value === "Annict") {
                // just save the details directly
                if (!userConfig.UserApiAuth)
                    userConfig.UserApiAuth = [];
                var existingConfig = userConfig.UserApiAuth.filter(i => i.Name === "Annict");
                if (existingConfig.length > 0) {
                    existingConfig[0].AccessToken = document.querySelector('#clientId').value.toString();
                } else {
                    userConfig.UserApiAuth.push({
                        "Name": "Annict",
                        "AccessToken": document.querySelector('#clientId').value.toString()
                    })
                }
            }

            ApiClient.updatePluginConfiguration(PluginConfig.pluginUniqueId, config).then(function (result) {
                Dashboard.processPluginConfigurationUpdateResult(result);
                ApiClient.getUsers()
                    .then(function (users) {
                        common.populateUserList(users, '#selectUser');
                        document.querySelector('#selectUser').value = userId;
                        loadUserConfiguration(userId);
                    })
                    .catch(error => console.log("Could not populate users list: " + error));
            });
        });
    }
}