export class RNG {
  private state: number;

  constructor(seed: number) {
    this.state = seed | 0 || 1;
  }

  public nextDouble(): number {
    let t = (this.state += 0x6d2b79f5);
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  }

  public next(minOrMax?: number, max?: number): number {
    if (minOrMax === undefined) {
      return Math.floor(this.nextDouble() * 0x7fffffff);
    }
    if (max === undefined) {
      return Math.floor(this.nextDouble() * minOrMax);
    }
    if (max <= minOrMax) {
      return minOrMax;
    }
    return minOrMax + Math.floor(this.nextDouble() * (max - minOrMax));
  }
}
