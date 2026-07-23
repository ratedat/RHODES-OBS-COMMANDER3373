import assert from "node:assert/strict";
import fs from "node:fs";
import test from "node:test";

import { renderBossCard, renderBossChip } from "../app/components/boss.js";
import {
  bossImages,
  buildBossFlagEntries,
  getBossManualSections,
  getSelectedManualBosses,
} from "../app/domain/boss-flags.js";

const campaigns = JSON.parse(fs.readFileSync(new URL("../data/campaigns.json", import.meta.url), "utf8"));
const relics = JSON.parse(fs.readFileSync(new URL("../data/relics.json", import.meta.url), "utf8")).relics;
const suiBossConfig = campaigns.find((campaign) => campaign.id === "is6_sui")?.bossFlags;
const relicMap = new Map(relics.map((relic) => [relic.id, relic]));
const cloudAndLacquerRelicId = "is6_sui_relic_215";
const izayoRelicId = "is6_sui_relic_220";

function buildSuiFlags(relicIds, manualBosses = []) {
  return buildBossFlagEntries({ config: suiBossConfig, relicIds, manualBosses, relicMap });
}

test("Sui END5 selection is hidden until Izayoi is owned", () => {
  const sections = getBossManualSections(suiBossConfig, []);

  assert.deepEqual(sections.map((section) => section.field), ["floor3BossId", "floor5BossId"]);
});

test("Cloud and Lacquer restores the Sui sixth-floor boss selection", () => {
  const sections = getBossManualSections(suiBossConfig, [cloudAndLacquerRelicId]);
  const floor6 = sections.find((section) => section.field === "floor6BossId");

  assert.deepEqual(sections.map((section) => section.field), ["floor3BossId", "floor5BossId", "floor6BossId"]);
  assert.ok(floor6);
  assert.equal(floor6.options.length, 1);
  assert.equal(floor6.options[0].id, "is6_route_end3_black_white");
  assert.equal(floor6.options[0].stageName, "歳を謀る者");
  assert.equal(floor6.options[0].image?.localPath, "assets/bosses/wikiru/img/WAN.jpg");
});

test("Cloud and Lacquer keeps one sixth-floor boss entry when it is also selected manually", () => {
  const sections = getBossManualSections(suiBossConfig, [cloudAndLacquerRelicId]);
  const manualBosses = getSelectedManualBosses(sections, {
    floor6BossId: "is6_route_end3_black_white",
  });
  const entries = buildSuiFlags([cloudAndLacquerRelicId], manualBosses);

  assert.equal(entries.length, 1);
  assert.equal(entries[0].id, "is6_route_end3_black_white");
  assert.equal(entries[0].triggerRelic?.id, cloudAndLacquerRelicId);
});

test("Izayoi exposes all seven Sui END5 route patterns", () => {
  const sections = getBossManualSections(suiBossConfig, [izayoRelicId]);
  const end5 = sections.find((section) => section.field === "end5BossVariantId");

  assert.ok(end5);
  assert.equal(end5.options.length, 7);
  assert.deepEqual(
    end5.options.map((option) => option.id),
    [
      "is6_end5_ding_qiankun_normal",
      "is6_end5_zhivian_appease",
      "is6_end5_zhivian_trace_form",
      "is6_end5_zhivian_change_game",
      "is6_end5_zhivian_old_calendar",
      "is6_end5_ding_qiankun_origin",
      "is6_end5_zhivian_beasts",
    ],
  );
  assert.ok(end5.options.every((option) => option.image?.localPath === "assets/bosses/wikiru/img/SQ.jpg"));
  assert.ok(end5.options.every((option) => !option.images));
});

test("relic ownership never auto-selects a Sui END5 boss pattern", () => {
  assert.equal(buildSuiFlags([izayoRelicId]).length, 0);
  assert.equal(buildSuiFlags([izayoRelicId, "is6_sui_relic_221", "is6_sui_relic_222"]).length, 0);
});

test("the selected Sui END5 route is the only route sent to boss output", () => {
  const sections = getBossManualSections(suiBossConfig, [izayoRelicId]);
  const manualBosses = getSelectedManualBosses(sections, {
    end5BossVariantId: "is6_end5_zhivian_beasts",
  });
  const entries = buildSuiFlags(
    [izayoRelicId, "is6_sui_relic_221", "is6_sui_relic_222"],
    manualBosses,
  );

  assert.equal(entries.length, 1);
  assert.equal(entries[0].stageName, "止変（群獣を役す）");
  assert.equal(entries[0].bossName, "易＋「歳」＋「望」");
  assert.equal(entries[0].requiredNote, "選択条件: 不赦＋不息");
  assert.deepEqual(
    bossImages(entries[0]).map((image) => image.localPath),
    ["assets/bosses/wikiru/img/SQ.jpg"],
  );
  assert.match(renderBossCard(entries[0]), /不赦＋不息/u);
  assert.match(renderBossChip(entries[0]), /不赦＋不息/u);
});

test("a saved END5 route is ignored while Izayoi is not owned", () => {
  const hiddenSections = getBossManualSections(suiBossConfig, []);
  const manualBosses = getSelectedManualBosses(hiddenSections, {
    end5BossVariantId: "is6_end5_zhivian_beasts",
  });

  assert.equal(manualBosses.length, 0);
});
