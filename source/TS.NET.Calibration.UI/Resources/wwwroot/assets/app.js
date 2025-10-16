document.addEventListener("alpine:init", () => {
    Alpine.store("header", {
        duration: "-",
        status: "-"
    });
    Alpine.store("footer", {
        contextHelp: "",
        zoom: "Zoom: 100%"
    });

    zoom();

    // Detect zoom changes
    window.addEventListener('resize', zoom);
    window.matchMedia('screen').addEventListener('change', zoom);
});

document.addEventListener("DOMContentLoaded", () => {
    const sequenceButton = document.getElementById("sequence-button");
    const startButton = document.getElementById("start-button");
    const stopButton = document.getElementById("stop-button");

    let startTimestamp;
    let durationInterval;

    function sendMessage(message) {
        window.external.sendMessage(JSON.stringify(message));
    }

    window.external.receiveMessage((message) => {
        const parsedMessage = JSON.parse(message);
        switch (parsedMessage.type) {
            case "log":
                updateLogView(parsedMessage);
                break;
            case "variables":
                updateVariablesView(parsedMessage);
                break;
            case "sequence":
                if (parsedMessage.name != null)
                    document.getElementById("sequence-dialog").classList.add("hidden");
                updateSequence(parsedMessage);
                break;
            case "log-update":
                const logContent = document.getElementById("log-content");
                addLogMessage(parsedMessage, logContent);
                break;
            case "sequence-status-update":
                updateSequenceStatus(parsedMessage.status);
                if (parsedMessage.status !== "Running") {
                    clearInterval(durationInterval);
                }
                break;
            case "step-update":
                updateStepStatus(parsedMessage.step);
                break;
        }
    });

    sendMessage({ command: "app-loaded" });

    function updateLogView(message) {
        const logContent = document.getElementById("log-content");
        logContent.innerHTML = "";

        message.log.forEach((logEvent) => {
            addLogMessage(logEvent, logContent);
        });
    }

    function updateSequence(message) {
        const sequenceTableBody = document.getElementById("sequence-table-body");
        sequenceTableBody.innerHTML = "";
        if (message.steps !== null) {
            message.steps.forEach((step) => {
                const row = document.createElement("tr");
                row.setAttribute("data-step-index", step.index);
                row.className = "border-b border-neutral-800";
                if (step.result == null) {
                    row.innerHTML = `
                    <td class="p-1">${step.index}</td>
                    <td class="p-1">${step.name}</td>
                    <td class="p-1">${step.skip ? "Yes" : "-"}</td>
                    <td class="p-1">${step.ignoreError ? "Yes" : "-"}</td>
                    <td class="p-1">-</td>
                    <td class="p-1">-</td>`;
                }
                else {
                    row.innerHTML = `
                    <td class="p-1">${step.index}</td>
                    <td class="p-1">${step.name}</td>
                    <td class="p-1">${step.skip ? "Yes" : "-"}</td>
                    <td class="p-1">${step.ignoreError ? "Yes" : "-"}</td>
                    <td class="p-1">${step.result.duration}</td>
                    <td class="p-1">${step.result.status}</td>`;
                    const statusCell = row.children[5];
                    statusColour(statusCell, step.result.status);
                }
                sequenceTableBody.appendChild(row);
            });
        }
        if (message.duration != null) {
            const headlineDurationValue = document.getElementById("headline-duration-value");
            headlineDurationValue.textContent = message.duration;
        }
        if (message.status != null) {
            const element = document.getElementById("headline-state-value");
            element.innerText = message.status;
            statusColour(element, message.status);
        }
    }

    function updateVariablesView(message) {
        var wrapper = document.getElementById("variables-content");
        wrapper.innerText = "";
        var tree = jsonTree.create(message.variables, wrapper);
    }

    function addLogMessage(message, container) {
        const div = document.createElement("div");
        div.className = "text-neutral-50";
        div.textContent = "[" + formatDateTime(message.timestamp) + "] " + message.message;
        container.appendChild(div);
        container.scrollTop = container.scrollHeight;
    }

    function updateSequenceStatus(status) {
        const element = document.getElementById("headline-state-value");
        element.innerText = status;
        statusColour(element, status);
    }

    function updateStepStatus(step) {
        const sequenceTableBody = document.getElementById("sequence-table-body");
        const row = sequenceTableBody.querySelector(
            `tr[data-step-index="${step.index}"]`
        );
        if (row) {
            row.scrollIntoView({ behavior: "smooth", block: "nearest" });
            const durationCell = row.children[4];
            const statusCell = row.children[5];

            durationCell.textContent = step.result.duration;
            statusCell.textContent = step.result.status;
            statusColour(statusCell, step.result.status);
        }
    }

    function statusColour(element, status) {
        element.classList.remove(
            "text-green-400",
            "text-red-400",
            "text-yellow-400",
            "text-neutral-100",
            "text-neutral-50"
        );
        switch (status) {
            case "Passed":
            case "Done":
                element.classList.add("text-green-400");
                break;
            case "Failed":
            case "Error":
                element.classList.add("text-red-400");
                break;
            case "Cancelled":
            case "Running":
                element.classList.add("text-yellow-400");
                break;
            case "Skipped":
            default:
                element.classList.add("text-neutral-50");
                break;
        }
    }

    function formatDateTime(isoString) {
        const date = new Date(isoString);

        const year = date.getFullYear();
        // getMonth() is zero-based, so add 1. Pad with '0' if needed.
        const month = (date.getMonth() + 1).toString().padStart(2, '0');
        const day = date.getDate().toString().padStart(2, '0');
        const hours = date.getHours().toString().padStart(2, '0');
        const minutes = date.getMinutes().toString().padStart(2, '0');
        const seconds = date.getSeconds().toString().padStart(2, '0');

        return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
    }

    function formatDuration(totalSeconds) {
        const hours = Math.floor(totalSeconds / 3600);
        const minutes = Math.floor((totalSeconds % 3600) / 60);
        const seconds = totalSeconds % 60;

        if (hours > 0) {
            return `${hours}h ${minutes}m ${seconds.toFixed(0)}s`;
        } else if (minutes > 0) {
            return `${minutes}m ${seconds.toFixed(0)}s`;
        } else {
            return `${seconds.toFixed(0)}s`;
        }
    }

    sequenceButton.addEventListener("click", () => {
        const selectedSequence = document.querySelector('input[name="sequence"]:checked').value;
        document.getElementById("sequence-dialog").classList.add("hidden");
        sendMessage({ command: "load-sequence", sequence: selectedSequence });
    });

    startButton.addEventListener("click", () => {
        sendMessage({ command: "start-sequence" });

        startTimestamp = new Date();
        const headlineDurationValue = document.getElementById("headline-duration-value");
        headlineDurationValue.textContent = "0s";

        durationInterval = setInterval(() => {
            const now = new Date();
            const elapsed = (now - startTimestamp) / 1000; // seconds
            headlineDurationValue.textContent = formatDuration(elapsed);
        }, 1000);
    });

    stopButton.addEventListener("click", () => {
        sendMessage({ command: "stop-sequence" });
    });
});

function help(text) {
    var displayText = "";
    if (text && text.length > 0)
        displayText = text;
    Alpine.store('footer').contextHelp = displayText;
}

function zoom() {
    const zoomLevel = Math.round(window.devicePixelRatio * 100);
    Alpine.store('footer').zoom = "Zoom: " + zoomLevel + "%";
}