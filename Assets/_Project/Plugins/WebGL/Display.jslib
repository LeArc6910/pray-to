// Browser facts the game needs that Unity does not expose, and a text box of the page's own. Called from Ui.cs,
// Controls.cs and WebText.cs in web builds only.
mergeInto(LibraryManager.library, {
  // CSS pixels per device pixel; Unity draws the canvas at the device's full resolution.
  GetDevicePixelRatio: function () {
    return window.devicePixelRatio || 1;
  },

  // Whether the main way of pointing is a finger (a phone or a tablet, an iPad too, whose browser says it is a Mac):
  // the screen is laid out for touch from the start, before the first touch.
  GetCoarsePointer: function () {
    return window.matchMedia && window.matchMedia("(pointer: coarse)").matches ? 1 : 0;
  },

  // ---- The page's own text box for the game's fields on a PC (WebText.cs) ----
  // Unity reads keys one key press at a time, so letters put together by an input method (Korean, Japanese, Chinese)
  // never reach its fields. While a field is being typed in, the keys go to a real text box of the page instead, laid
  // over the field so the input method's window opens beside it; the game draws its value. The page (index.html) keeps
  // Unity's key listener off the box and notes Enter, Esc and Tab on it (gameKey).

  TextInputOpen: function (textPtr, max, x, y, width, height) {
    var input = document.getElementById("game-text");
    if (!input) {
      input = document.createElement("input");
      input.id = "game-text";
      input.type = "text";
      input.autocomplete = "off";
      input.spellcheck = false;
      input.setAttribute("data-game-text", "1");
      input.style.cssText = "position:fixed;margin:0;padding:0;border:0;outline:none;opacity:0;background:transparent;font-size:16px;pointer-events:none;";
      document.body.appendChild(input);
    }
    input.style.left = x + "px";
    input.style.top = y + "px";
    input.style.width = Math.max(1, width) + "px";
    input.style.height = Math.max(1, height) + "px";
    input.maxLength = max > 0 ? max : 524288;
    input.value = UTF8ToString(textPtr);
    input.gameKey = 0;
    input.focus();
    var end = input.value.length;
    input.setSelectionRange(end, end);
  },

  // What is in the box now, letters still being put together included.
  TextInputValue: function () {
    var input = document.getElementById("game-text");
    var text = input ? input.value : "";
    var size = lengthBytesUTF8(text) + 1;
    var buffer = _malloc(size);
    stringToUTF8(text, buffer, size);
    return buffer;
  },

  // -1 when the box has lost the keys (a click elsewhere, another window), else the last of Enter (1), Esc (2) and Tab
  // (3) pressed in it since the last call, or 0.
  TextInputState: function () {
    var input = document.getElementById("game-text");
    if (!input || document.activeElement !== input) return -1;
    var key = input.gameKey || 0;
    input.gameKey = 0;
    return key;
  },

  // The keys back to the game.
  TextInputClose: function () {
    var input = document.getElementById("game-text");
    if (input && document.activeElement === input) {
      input.blur();
      if (Module.canvas) Module.canvas.focus();
    }
  },
});
