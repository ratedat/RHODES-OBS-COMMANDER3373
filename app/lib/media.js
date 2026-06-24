import { assetUrl } from "./format.js";

export function mediaUrl(image) {
  return image?.localPath ? assetUrl(image.localPath) : (image?.sourceUrl || "");
}

export function specialEffectImageSrc(item) {
  return mediaUrl(item?.image);
}
