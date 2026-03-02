// Set up event handlers
const reconnectModal = document.getElementById("components-reconnect-modal");
const retryButton = document.getElementById("components-reconnect-button");
const resumeButton = document.getElementById("components-resume-button");

const reconnectStateClasses = [
    "components-reconnect-show",
    "components-reconnect-failed",
    "components-reconnect-rejected"
];

if (reconnectModal) {
    reconnectModal.addEventListener("components-reconnect-state-changed", handleReconnectStateChanged);
    if (retryButton) {
        retryButton.addEventListener("click", retry);
    }
    if (resumeButton) {
        resumeButton.addEventListener("click", resume);
    }
}

function handleReconnectStateChanged(event) {
    const state = event?.detail?.state;

    if (state === "show") {
        setModalState("components-reconnect-show");
        reconnectModal.showModal?.();
    } else if (state === "hide") {
        clearModalState();
        reconnectModal.close?.();
        document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    } else if (state === "failed") {
        setModalState("components-reconnect-failed");
        reconnectModal.showModal?.();
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    } else if (state === "rejected") {
        setModalState("components-reconnect-rejected");
        reconnectModal.showModal?.();
    }
}

async function retry() {
    document.removeEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    setModalState("components-reconnect-show");

    try {
        const successful = await Blazor.reconnect();
        if (!successful) {
            setModalState("components-reconnect-failed");
            return;
        }

        clearModalState();
        reconnectModal?.close?.();
    } catch (err) {
        setModalState("components-reconnect-failed");
        document.addEventListener("visibilitychange", retryWhenDocumentBecomesVisible);
    }
}

async function resume() {
    await retry();
}

async function retryWhenDocumentBecomesVisible() {
    if (document.visibilityState === "visible") {
        await retry();
    }
}

function setModalState(activeClass) {
    if (!reconnectModal) {
        return;
    }

    for (const stateClass of reconnectStateClasses) {
        reconnectModal.classList.remove(stateClass);
    }

    reconnectModal.classList.add(activeClass);
}

function clearModalState() {
    if (!reconnectModal) {
        return;
    }

    for (const stateClass of reconnectStateClasses) {
        reconnectModal.classList.remove(stateClass);
    }
}
