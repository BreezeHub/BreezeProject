export class CycleInfo {

    constructor(periodStart: number, periodEnd: number, height: number, blocksLeft: number, cycleStart: number, cycleFailed: boolean, cycleAsciiArt: string, cycleStatus: string, cyclePhase: string, cyclePhaseNumber: number) {
      this.periodStart = periodStart;
      this.periodEnd = periodEnd;
      this.height = height;
      this.blocksLeft = blocksLeft;
      this.cycleStart = cycleStart;
      this.cycleFailed = cycleFailed;
      this.cycleAsciiArt = cycleAsciiArt;
      this.cycleStatus = cycleStatus;
      this.cyclePhase = cyclePhase;
      this.cyclePhaseNumber = cyclePhaseNumber;
    }

    periodStart: number;
    periodEnd: number;
    height: number;
    blocksLeft: number;
    cycleStart: number;
    cycleFailed: boolean;
    cycleAsciiArt: string;
    cycleStatus: string;
    cyclePhase: string;
    cyclePhaseNumber: number;
  }
