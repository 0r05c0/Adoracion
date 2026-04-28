fetch('https://api.github.com/repos/0r05c0/Adoracion/releases/latest')
            .then(res => res.json())
            .then(data => {
                if (data.tag_name) {
                    var lastVersion = data.tag_name;
                    var downloadLink = document.getElementById("download-link");
                    downloadLink.href = "https://github.com/0r05c0/Adoracion/releases/download/" + lastVersion + "/Adoracion-win-x64.zip";
                    document.getElementById('version-number').innerText = lastVersion;
                    document.getElementById('version-link').innerText = lastVersion;
                }
            }).catch(console.error);