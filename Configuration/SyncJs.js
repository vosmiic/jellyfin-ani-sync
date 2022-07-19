var PluginConfig = {
    pluginUniqueId: 'c78f11cf-93e6-4423-8c42-d2c255b70e47'
};

export default function (view, params) {
    const commonsUrl = ApiClient.getUrl('web/ConfigurationPage', {name: 'GeneralJs'})

    view.addEventListener('viewshow', async function (e) {
        import(commonsUrl).then(initialLoad.bind(this));
    });
    
    view.querySelector('#selectAction').addEventListener('change', actionSelectionChange)
}

async function initialLoad(commons) {
    const page = this;
    LibraryMenu.setTabs('Ani-Sync', 1, commons.getTabs);
    
    ApiClient.getUsers().then(async function (users) {
        populateUserList(page, users);
    });
    await setParameters(page);
    page.querySelector('#run').onclick = run;
}

function populateUserList(page, users) {
    var html = '';
    for (var x = 0; x < users.length; x++) {
        html += '<option value="' + users[x].Id + '">' + users[x].Name + '</option>';
    }
    page.querySelector('#selectUser').innerHTML = html;
}

async function setParameters(page) {
    await fetch(ApiClient.getUrl("/AniSync/parameters"), {
        method: "GET"
    }).then(function (response) {
        if (response.ok) {
            return response.json()
                .then(function (json) {
                    setProviderSelection(page, json.providerList);
                });
        }
    });
}

function setProviderSelection(page, providerList) {
    var html = '';
    for (var x = 0; x < providerList.length; x++) {
        html += '<option value="' + providerList[x].Key + '">' + providerList[x].Name + '</option>';
    }
    page.querySelector('#selectProvider').innerHTML = html;
}

function actionSelectionChange(value) {
    switch (value.target.value) {
        case "UpdateProvider":
            document.getElementById('selectProvider').disabled = true;
            document.getElementById('status').disabled = true;
            break;
        case "UpdateJellyfin":
            document.getElementById('selectProvider').disabled = false;
            document.getElementById('status').disabled = false;
            break;
    }
}

    
async function run() {
    await fetch(ApiClient.getUrl("/AniSync/sync?provider=" + document.getElementById('selectProvider').value + "&userId=" + document.getElementById('selectUser').value + "&status=" + document.getElementById('status').value + "&syncAction=" + document.getElementById('selectAction').value), {
        method: "POST"
    });
}