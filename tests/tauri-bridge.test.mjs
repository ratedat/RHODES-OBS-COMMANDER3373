import test from "node:test";
import assert from "node:assert/strict";
import { getTauriInvoke, isTauriRuntime, readTauriStorageTarget } from "../app/lib/tauri-bridge.js";

test("Tauri bridge stays inactive outside Tauri", async () => {
  const root = {};
  assert.equal(getTauriInvoke(root), null);
  assert.equal(isTauriRuntime(root), false);
  assert.equal(await readTauriStorageTarget(root), null);
});

test("Tauri bridge invokes the storage target command through the v2 global API", async () => {
  const calls = [];
  const root = {
    __TAURI__: {
      core: {
        invoke: async (command) => {
          calls.push(command);
          return { stateDir: "D:/RHODES/state" };
        },
      },
    },
  };
  assert.equal(isTauriRuntime(root), true);
  assert.deepEqual(await readTauriStorageTarget(root), { stateDir: "D:/RHODES/state" });
  assert.deepEqual(calls, ["rhodes_storage_target"]);
});
