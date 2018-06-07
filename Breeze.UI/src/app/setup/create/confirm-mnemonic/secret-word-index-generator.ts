export class SecretWordIndexGenerator {
    
    private readonly textPrefix = 'Word °';
    readonly index1 = SecretWordIndexGenerator.getRandom();
    readonly index2 = SecretWordIndexGenerator.getRandom(this.index1);
    readonly index3 = SecretWordIndexGenerator.getRandom(this.index1, this.index2);
    readonly text1 = `${this.textPrefix}${this.index1 +1 }`;
    readonly text2 = `${this.textPrefix}${this.index2 +1 }`;
    readonly text3 = `${this.textPrefix}${this.index3 +1 }`; 

    private static getRandom(...taken): number {
        const min = 0, max = 11;
        const getRandom = () => Math.floor(Math.random() * (max - min + 1) + min);
        var random = 0;
        while (taken.includes(random = getRandom())) { }
        return random;
    }
}
