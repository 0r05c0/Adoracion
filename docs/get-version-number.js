fetch('https://api.github.com/repos/0r05c0/Adoracion/releases/latest')
            .then(res => res.json())
            .then(data => {
                if (data.tag_name) {
                    document.getElementById('version-number').innerText = data.tag_name;
                    document.getElementById('version-link').innerText = data.tag_name;
                }
            }).catch(console.error);