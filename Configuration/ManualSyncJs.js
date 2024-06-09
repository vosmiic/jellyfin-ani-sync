export default function (view, params) {
    view.addEventListener('viewshow', async function () {
        var generalFunctionsUrl = ApiClient.getUrl("web/ConfigurationPage", { name: "AniSync_CommonJs" });
        import(generalFunctionsUrl).then(async (common) => await initialLoad(common))
    });
}
async function initialLoad(common) {
    const page = document;
    common.setTabs(common.TabManualSync, common.getTabs);
    
    page.querySelector('#run').onclick = run;

    page.querySelector('#selectAction').addEventListener('change', function (q) {
        actionSelectionChange(page, q);
    });

    await setParameters(common, page);

    ApiClient.getUsers()
        .then(function (users) {
            common.populateUserList(page, users, '#selectSyncUser');
        })
        .catch(error => console.log("Could not populate users list: " + error));
    
    function actionSelectionChange(page, value) {
        switch (value.target.value) {
            case "UpdateProvider":
                page.querySelector('#selectSyncProvider').disabled = true;
                page.querySelector('#status').disabled = true;
                break;
            case "UpdateJellyfin":
                page.querySelector('#selectSyncProvider').disabled = false;
                page.querySelector('#status').disabled = false;
                break;
        }
    }
}

async function run() {
    var url = ApiClient.getUrl("/AniSync/sync?provider=" + document.querySelector('#selectSyncProvider').value + "&userId=" + encodeURIComponent(document.querySelector('#selectSyncUser').value) + "&status=" + encodeURIComponent(document.querySelector('#status').value) + "&syncAction=" + encodeURIComponent(document.querySelector('#selectAction').value));
    await ApiClient.ajax({
        type: "POST",
        url
    });
}

async function setParameters(common, page) {
    var url = ApiClient.getUrl("/AniSync/parameters?includes=" + common.parameterInclude.ProviderList);
    await ApiClient.ajax({type: 'GET', url})
        .then(function (response) {
            if (response.ok) {
                return response.json()
                    .then(function (json) {
                        common.setProviderSelection(page, json.providerList, '#selectSyncProvider');
                    });
            } else {
                page.querySelector('#localApiUrl').innerHTML = "Could not fetch local URL.";
            }
        });
}