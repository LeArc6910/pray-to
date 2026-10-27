// Browser facts the game needs that Unity does not expose. Called from Ui.cs in web builds only.
mergeInto(LibraryManager.library, {
  // CSS pixels per device pixel; Unity draws the canvas at the device's full resolution.
  GetDevicePixelRatio: function () {
    return window.devicePixelRatio || 1;
  },
});
