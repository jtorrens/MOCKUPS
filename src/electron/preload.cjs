const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("mockupsNative", {
  pickFile: () => ipcRenderer.invoke("mockups:pickFile"),
  listFonts: () => ipcRenderer.invoke("mockups:listFonts"),
});

