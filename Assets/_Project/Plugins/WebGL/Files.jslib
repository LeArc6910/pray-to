// Files to and from the player's device, for carrying saves off and back (SaveTransfer.cs, Docs/Saving.md). Web
// builds only. A browser opens its file picker only from a press on the page itself, and Unity's buttons are drawn
// inside the canvas, so picking shows a small box over the game with a real button to press.
mergeInto(LibraryManager.library, {
  // The bytes as a download of this name.
  SaveFileToDevice: function (namePtr, dataPtr, length) {
    var name = UTF8ToString(namePtr);
    var blob = new Blob([HEAPU8.slice(dataPtr, dataPtr + length)], { type: "application/octet-stream" });
    var url = URL.createObjectURL(blob);
    var link = document.createElement("a");
    link.href = url;
    link.download = name;
    link.style.display = "none";
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    setTimeout(function () { URL.revokeObjectURL(url); }, 60000);
  },

  // Shows the box: a line of text, a button that opens the picker and one that gives up. The chosen file is kept
  // for PickFileTake. Nothing happens if the box is already up.
  PickFileStart: function (textPtr, choosePtr, cancelPtr, maxBytes) {
    var picker = window.prayToPicker || (window.prayToPicker = { state: 0, bytes: null, box: null });
    if (picker.box) return;
    picker.state = 1;
    picker.bytes = null;

    var box = document.createElement("div");
    box.style.cssText = "position:fixed;left:0;top:0;width:100%;height:100%;display:flex;align-items:center;" +
      "justify-content:center;background:rgba(0,0,0,0.6);z-index:10;font:16px sans-serif;color:#EFE6D6";
    var panel = document.createElement("div");
    panel.style.cssText = "background:#17120F;border:2px solid #7A6450;padding:16px;max-width:80%;text-align:center";
    var text = document.createElement("p");
    text.style.cssText = "margin:0 0 14px 0";
    text.textContent = UTF8ToString(textPtr);
    var buttonStyle = "display:inline-block;margin:4px;padding:10px 16px;border:2px solid #7A6450;" +
      "background:#3A2C22;color:#EFE6D6;font:16px sans-serif;cursor:pointer";
    // The input sits inside its label, so pressing the label is pressing the input. Hidden by size, not by
    // display:none, which some phones then ignore.
    var choose = document.createElement("label");
    choose.style.cssText = buttonStyle + ";position:relative;overflow:hidden";
    choose.textContent = UTF8ToString(choosePtr);
    var input = document.createElement("input");
    input.type = "file";
    input.style.cssText = "position:absolute;left:0;top:0;width:1px;height:1px;opacity:0";
    choose.appendChild(input);
    var cancel = document.createElement("button");
    cancel.style.cssText = buttonStyle;
    cancel.textContent = UTF8ToString(cancelPtr);

    function close(state) {
      picker.state = state;
      document.removeEventListener("keydown", onKey, true);
      if (box.parentNode) box.parentNode.removeChild(box);
      picker.box = null;
    }
    function onKey(e) {
      if (e.key === "Escape") close(3);
    }
    input.addEventListener("change", function () {
      var file = input.files && input.files[0];
      if (!file) return;
      if (file.size > maxBytes) {
        close(4);
        return;
      }
      var reader = new FileReader();
      reader.onload = function () {
        picker.bytes = new Uint8Array(reader.result);
        close(2);
      };
      reader.onerror = function () { close(4); };
      reader.readAsArrayBuffer(file);
    });
    cancel.addEventListener("click", function () { close(3); });
    document.addEventListener("keydown", onKey, true);

    panel.appendChild(text);
    panel.appendChild(choose);
    panel.appendChild(cancel);
    box.appendChild(panel);
    document.body.appendChild(box);
    picker.box = box;
  },

  // 0 nothing asked, 1 the box is up, 2 a file is ready (PickFileLength, PickFileTake), 3 given up, 4 the file was
  // too big or could not be read. 3 and 4 are told once, then it is 0 again.
  PickFileState: function () {
    var picker = window.prayToPicker;
    if (!picker) return 0;
    var state = picker.state;
    if (state === 3 || state === 4) picker.state = 0;
    return state;
  },

  PickFileLength: function () {
    var picker = window.prayToPicker;
    return picker && picker.bytes ? picker.bytes.length : 0;
  },

  // Copies the chosen file into the game's buffer and forgets it.
  PickFileTake: function (bufferPtr, length) {
    var picker = window.prayToPicker;
    if (!picker || !picker.bytes) return;
    HEAPU8.set(picker.bytes.subarray(0, length), bufferPtr);
    picker.bytes = null;
    picker.state = 0;
  },
});
