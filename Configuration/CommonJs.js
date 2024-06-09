// below tab functionality is heavily influenced by the tabs used in jellyfin-plugin-media-cleaner: https://github.com/shemanaev/jellyfin-plugin-media-cleaner

export function getTabs() {
    const tabs = [
        {
            href: configurationPageUrl('Ani-Sync'),
            name: 'General'
        },
        {
            href: configurationPageUrl('AniSync_ManualSync'),
            name: 'Manual Sync'
        }
    ];
    return tabs;
}

export function setTabs(selectedIndex, itemsFn) {
    const $tabs = document.querySelector('.pluginConfigurationPage:not(.hide) #navigationTabs');
    $tabs.innerHTML = '';

    let i = 0;
    for (const tab of itemsFn()) {
        const elem = document.createElement("a");
        elem.innerHTML = tab.name;
        elem.addEventListener('click', (e) => Dashboard.navigate('/' + tab.href, false));
        elem.className = 'emby-button' + (i === selectedIndex ? ' ui-btn-active' : '');
        elem.dataset.role = 'button';

        i++;
        $tabs.appendChild(elem);
    }
}

export const TabGeneral = 0;
export const TabManualSync = 1;

const configurationPageUrl = (name) => 'configurationpage?name=' + encodeURIComponent(name);

export function setProviderSelection(page, providerList, providerListSelectElement) {
    var html = '';
    for (var x = 0; x < providerList.length; x++) {
        html += '<option value="' + providerList[x].Key + '">' + providerList[x].Name + '</option>';
    }
    page.querySelector(providerListSelectElement).innerHTML = html;
}

export function populateUserList(page, users, userListSelectElement) {
    var html = '';
    for (var x = 0; x < users.length; x++) {
        html += '<option value="' + users[x].Id + '">' + users[x].Name + '</option>';
    }
    page.querySelector(userListSelectElement).innerHTML = html;
}

export const parameterInclude = {
    ProviderList: 0,
    LocalIpAddress: 1,
    LocalPort: 2,
    Https: 3
}