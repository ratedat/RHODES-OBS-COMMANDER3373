export function createSaveRequestTracker() {
  let version = 0;

  return {
    issue() {
      version += 1;
      return version;
    },
    invalidate() {
      version += 1;
      return version;
    },
    isCurrent(token) {
      return token === version;
    },
  };
}
