/** Runs due work in timestamp order, including retries scheduled by that work. */
export class SimulationClock {
  private jobs: { at: number; run: () => void }[] = [];
  at = 0;
  constructor(private now = () => Date.now()) {}

  schedule(delay: number, run: () => void) {
    this.jobs.push({ at: this.at + delay, run });
  }

  advance() {
    const now = this.now();
    this.jobs.sort((a, b) => a.at - b.at);
    while (this.jobs[0]?.at <= now) {
      const job = this.jobs.shift()!;
      this.at = job.at;
      job.run();
      this.jobs.sort((a, b) => a.at - b.at);
    }
    this.at = now;
  }
}
