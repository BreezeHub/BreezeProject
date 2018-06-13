export interface ITransaction {
    amount: number,
    confirmedInBlock: number,
    fee?: number;
    id: string,
    payments: Array<any>
    timestamp: string,
    toAddress: string,
    type: string
}