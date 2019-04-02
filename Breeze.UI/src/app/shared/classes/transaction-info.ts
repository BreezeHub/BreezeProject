import { Transaction } from '../../../interfaces/transaction';

export class TransactionInfo {

  constructor(transaction: Transaction) {
    this.transactionType = transaction.type;
    this.transactionId = transaction.id;
    this.transactionAmount = transaction.amount;
    this.transactionFee = transaction.fee || 0;
    this.transactionConfirmedInBlock = transaction.confirmedInBlock;
    this.transactionTimestamp = transaction.timestamp;
  }

  public transactionType: string;
  public transactionId: string;
  public transactionAmount: number;
  public transactionFee: number;
  public transactionConfirmedInBlock?: number;
  public transactionTimestamp: string;
}
