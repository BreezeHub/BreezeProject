import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';

@Injectable()
export class LicenseService {
    
    private _licenseText: string;
    
    constructor(http: HttpClient) {
        http.get("assets/images/license_en.txt", { responseType: 'text' }).subscribe(x => this._licenseText = x);
    }

    get licenseText(): string {
        return this._licenseText;
    }
}
