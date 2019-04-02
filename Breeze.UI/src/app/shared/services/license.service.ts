import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Injectable({
  providedIn: 'root'
})
export class LicenseService {

    private _licenseText: string;

    constructor(http: HttpClient) {

        this._licenseText = `
        By  installing  this software the user agrees to all of the following:

        That this software is an experimental release and any use of it shall be at the users own discretion and risk.
        That  the  sole and exclusive remedy for any problem(s), malfunctions  or defects in the product, software  and / or service shall be to uninstall and/or  to stop using it .
        In no event shall Stratis, its officers, shareholders, investors, employees, agents, directors, subsidiaries, affiliates, successors, assignees or suppliers be liable for
        Any indirect, incidental, punitive, exemplary or consequential damages
        Any loss of data, use, sales, business (including profits) whether direct or indirect in any and all cases arising out of use or inability to use the software including any form of security breach, hack or attack.
        Any loss, errors, omissions or misplacement of coins or any other digital asset transacted through the software or otherwise.
        Any transaction confirmation delays or lack of completions.
        The product is provided on an "as is" basis without any representation or warranty, whether express, implied or otherwise to the maximum extent permitted by applicable law including fitness for a particular purpose, merchantability and defects.
        The product shall not be used for any unlawful activity that would violate or assist in violation of any applicable law, regulation or policy.
        `;
    }

    get licenseText(): string {
        return this._licenseText;
    }
}
