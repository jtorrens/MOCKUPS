const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("mockupsNative", {
  pickFile: () => ipcRenderer.invoke("mockups:pickFile"),
  pickDirectory: () => ipcRenderer.invoke("mockups:pickDirectory"),
  mediaDataUrl: (filePath, rootPath) =>
    ipcRenderer.invoke("mockups:mediaDataUrl", filePath, rootPath),
  listFonts: () => ipcRenderer.invoke("mockups:listFonts"),
});
