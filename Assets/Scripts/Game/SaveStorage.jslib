// WebGL 빌드의 저장소. SaveData.cs가 부른다(구현 계획 11절).
// 브라우저 localStorage에 키 하나로 JSON 문자열을 읽고 쓴다.
// 사생활 보호 모드나 저장소가 막힌 iframe에서는 localStorage가 예외를 던질 수 있다.
// 그때는 조용히 넘어가고(읽기는 빈 문자열), 게임은 저장 없이 계속된다.
mergeInto(LibraryManager.library, {
  SheepftStorageGet: function (keyPtr) {
    var value = "";
    try {
      var stored = window.localStorage.getItem(UTF8ToString(keyPtr));
      if (stored !== null) value = stored;
    } catch (e) {}
    var size = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(size);
    stringToUTF8(value, buffer, size);
    return buffer;
  },

  SheepftStorageSet: function (keyPtr, valuePtr) {
    try {
      window.localStorage.setItem(UTF8ToString(keyPtr), UTF8ToString(valuePtr));
    } catch (e) {}
  },

  SheepftStorageRemove: function (keyPtr) {
    try {
      window.localStorage.removeItem(UTF8ToString(keyPtr));
    } catch (e) {}
  }
});
