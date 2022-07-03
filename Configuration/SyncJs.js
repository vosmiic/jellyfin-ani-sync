var PluginConfig = {
    pluginUniqueId: 'c78f11cf-93e6-4423-8c42-d2c255b70e47'
};

export default function (view, params) {
    const commonsUrl = ApiClient.getUrl('web/ConfigurationPage', {name: 'GeneralJs'})

    view.addEventListener('viewshow', async function (e) {
        import(commonsUrl).then(initialLoad.bind(this));
    });
}

async function initialLoad(commons) {
    LibraryMenu.setTabs('Ani-Sync', 1, commons.getTabs);

    console.log(1);
    
    ApiClient.getUsers().then(async function (users) {
        populateUserList(users);
    });
    await setParameters();
}

function populateUserList(users) {
    var html = '';
    for (var x = 0; x < users.length; x++) {
        html += '<option value="' + users[x].Id + '">' + users[x].Name + '</option>';
    }
    document.querySelector('#selectUser').innerHTML = html;
}

async function setParameters() {
    await fetch(ApiClient.getUrl("/AniSync/parameters"), {
        method: "GET"
    }).then(function (response) {
        if (response.ok) {
            return response.json()
                .then(function (json) {
                    setProviderSelection(json.providerList);
                });
        }
    });
}

function setProviderSelection(providerList) {
    var html = '';
    for (var x = 0; x < providerList.length; x++) {
        html += '<option value="' + providerList[x].Key + '">' + providerList[x].Name + '</option>';
    }
    document.querySelector('#selectProvider').innerHTML = html;
}

document.querySelector('#TemplateConfigForm')
    .addEventListener('submit', async function (e) {
        e.preventDefault();
        await fetch(ApiClient.getUrl("/AniSync/syncFromProviders?provider=" + document.getElementById('selectProvider').value + "&userId=" + document.getElementById('selectUser').value + "&status=" + document.getElementById('status').value), {
            method: "POST"
        });
        return false;
    });