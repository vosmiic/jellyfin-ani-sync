var PluginConfig = {
    pluginUniqueId: 'c78f11cf-93e6-4423-8c42-d2c255b70e47'
};

export default function (view, params) {
    const commonsUrl = ApiClient.getUrl('web/ConfigurationPage', { name: 'GeneralJs' })

    view.addEventListener('viewshow', async function (e) {
        import(commonsUrl).then(initialLoad.bind(this));
    });
}

async function initialLoad(commons) {
    const page = this;
    LibraryMenu.setTabs('Ani-Sync', 0, commons.getTabs);
    Dashboard.showLoadingMsg();

    
    ApiClient.getUsers().then(function (users) {
        populateUserList(page, users);
        loadUserConfiguration(page.querySelector('#selectUser').value);
        setUserAddress(page);
    });

    await setParameters(page);
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
            saveUserConfig();
            e.preventDefault();
            return false;
        });

    page.querySelector('#testAnimeListSaveLocation').onclick = runTestAnimeListSaveLocation;
    page.querySelector('#generateCallbackUrlButton').onclick = generateCallbackUrl;
    page.querySelector('#authorizeDevice').onclick = onAuthorizeButtonClick;
    page.querySelector('#testAuthentication').onclick = getUser;
}


function runTestAnimeListSaveLocation() {
    document.querySelector('#testAnimeListSaveLocationResponse').innerHTML = "Testing anime list save location..."

    fetch(ApiClient.getUrl("/AniSync/testAnimeListSaveLocation?saveLocation=" + document.querySelector('#animeListSaveLocation').value), {
        method: "GET"
    }).then((response) => response.json())
        .then((result) => {
            if (result === "") {
                document.querySelector('#testAnimeListSaveLocationResponse').innerHTML = "Anime list save location is valid! Please remember to save"
            } else {
                document.querySelector('#testAnimeListSaveLocationResponse').innerHTML = "Error: " + result;
            }
        });
}


function generateCallbackUrl() {
    const userApiUrl = document.querySelector('#apiUrl').value;
    if (userApiUrl) {
        document.querySelector('#generalCallbackUrlInput').value = userApiUrl + "/AniSync/authCallback"
    } else {
        document.querySelector('#generalCallbackUrlInput').value = PluginConfig.localApiUrl + "/AniSync/authCallback"
    }
}


function onAuthorizeButtonClick() {
    // users are unlikely to save after setting client id and secret, so we do it for them
    saveUserConfig(true);
    if (document.querySelector('#selectProvider').value === "Kitsu") {
        fetch(ApiClient.getUrl("/AniSync/passwordGrant?provider=Kitsu&userId=" + document.querySelector('#selectUser').value +
            "&username=" + document.querySelector('#clientId').value +
            "&password=" + document.querySelector('#clientSecret').value), {
            method: "GET"
        })
    } else {
        fetch(ApiClient.getUrl("/AniSync/buildAuthorizeRequestUrl?provider=" + document.querySelector('#selectProvider').value + "&clientId=" + document.querySelector('#clientId').value +
            "&clientSecret=" + document.querySelector('#clientSecret').value +
            "&url=" + (document.querySelector('#apiUrl').value ? document.querySelector('#apiUrl').value : "local")), {
            method: "GET"
        })
            .then((response) => response.json())
            .then((json) => document.querySelector('#authorizeLink').innerHTML = json);
    }
}


function getUser() {
    document.querySelector('#getUserResponse').innerHTML = "Testing authentication.. this can take some time."
    fetch(ApiClient.getUrl("/AniSync/user?apiName=" + document.querySelector('#selectProvider').value +
        "&userId=" + document.querySelector('#selectUser').value), {
        method: "GET"
    })
        .then(function (response) {
            if (response.ok) {
                return response.json()
                    .then((json) => document.querySelector('#getUserResponse').innerHTML = "Thank you for authenticating " + json.name + ".");
            } else {
                document.querySelector('#getUserResponse').innerHTML = "Test returned an error - try authenticating again or check the logs for a detailed error reason."
            }
        });
}

async function setParameters(page) {
    await fetch(ApiClient.getUrl("/AniSync/parameters"), {
        method: "GET"
    }).then(function (response) {
        if (response.ok) {
            return response.json()
                .then(function (json) {
                    setLocalApiUrl(page, json.localApiUrl);
                    setProviderSelection(page, json.providerList);
                });
        } else {
            page.querySelector('#localApiUrl').innerHTML = "Could not fetch local URL.";
        }
    });
}

function setLocalApiUrl(page, localApiUrl) {
    PluginConfig.localApiUrl = localApiUrl;
    page.querySelector('#localApiUrl').innerHTML = "Local (server) URL: <b>" + localApiUrl + "</b>";
}

function setProviderSelection(page, providerList) {
    var html = '';
    for (var x = 0; x < providerList.length; x++) {
        html += '<option value="' + providerList[x].Key + '">' + providerList[x].Name + '</option>';
    }
    page.querySelector('#selectProvider').innerHTML = html;
}

function setUserAddress(page) {
    page.querySelector('#userAddress').innerHTML = "User URL: <b>" + ApiClient.serverAddress() + "</b>";
}

function populateUserList(page, users) {
    var html = '';
    for (var x = 0; x < users.length; x++) {
        html += '<option value="' + users[x].Id + '">' + users[x].Name + '</option>';
    }
    page.querySelector('#selectUser').innerHTML = html;
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

        if (providerName === "Kitsu") {
            page.querySelector('#clientIdLabel').innerHTML = "Username";
            page.querySelector('#clientSecretLabel').innerHTML = "Password";
        } else {
            page.querySelector('#clientIdLabel').innerHTML = "Client ID";
            page.querySelector('#clientSecretLabel').innerHTML = "Client Secret";
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

function saveUserConfig(saveTempAuth) {
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

        userConfig.LibraryToCheck = Array.prototype.map.call(document.querySelectorAll('.library:checked'), element => {
            return element.getAttribute('id');
        });
        userConfig.UserId = userId;
        userConfig.PlanToWatchOnly = document.querySelector('#PlanToWatchOnly').checked;
        userConfig.RewatchCompleted = document.querySelector('#RewatchCompleted').checked;

        if (saveTempAuth) {
            config.currentlyAuthenticatingUser = userId;
            config.currentlyAuthenticatingProvider = document.querySelector('#selectProvider').value;
        }

        ApiClient.updatePluginConfiguration(PluginConfig.pluginUniqueId, config).then(function (result) {
            Dashboard.processPluginConfigurationUpdateResult(result);
            ApiClient.getUsers().then(function (users) {
                populateUserList(users);
                document.querySelector('#selectUser').value = userId;
                loadUserConfiguration(userId);
            });
        });
    });
}