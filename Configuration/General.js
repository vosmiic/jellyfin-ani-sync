export function getTabs() {
    const tabs = [
        {
            href: getConfigurationPageUrl('Ani-Sync'),
            name: 'Ani-Sync'
        },
        {
            href: getConfigurationPageUrl('Sync'),
            name: 'Sync'
        }
    ]
    return tabs
}

console.log(1);

const getConfigurationPageUrl = (name) => ApiClient.getUrl('web/configurationpage', { name })