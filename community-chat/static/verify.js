"use strict";
window.addEventListener("load", async () => {
    const notify = value => window.chrome?.webview?.postMessage(value);
    try {
        if (!window.turnstile) throw new Error("Verification unavailable");
        const response = await fetch("/config", {cache: "no-store"});
        if (!response.ok) throw new Error("Configuration unavailable");
        const config = await response.json();
        turnstile.render("#verification", {
            sitekey: config.sitekey, action: "chat",
            callback: token => notify({token}),
            "error-callback": () => {notify({error: "Verification failed"}); return true;},
            "expired-callback": () => notify({error: "Verification expired"})
        });
    } catch { notify({error: "Verification unavailable"}); }
});
