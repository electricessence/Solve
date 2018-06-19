///<reference types="@aspnet/signalr"/>
var connection = new signalR.HubConnectionBuilder()
    .withUrl("/stats")
    .build();
connection.on("ReceiveMessage", function (user, message) {
    //const msg = message.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    //const encodedMsg = user + " says " + msg;
    //const li = document.createElement("li");
    //li.textContent = encodedMsg;
    //document.getElementById("messagesList").appendChild(li);
});
connection.start().catch(function (err) { return console.error(err.toString()); });
//document.getElementById("sendButton").addEventListener("click", event => {
//    const user = document.getElementById("userInput").value;
//    const message = document.getElementById("messageInput").value;
//    connection.invoke("SendMessage", user, message).catch(err => console.error(err.toString()));
//    event.preventDefault();
//});
//# sourceMappingURL=site.js.map