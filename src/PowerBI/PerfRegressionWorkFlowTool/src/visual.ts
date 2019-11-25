/*
*  Power BI Visual CLI
*
*  Copyright (c) Microsoft Corporation
*  All rights reserved.
*  MIT License
*
*  Permission is hereby granted, free of charge, to any person obtaining a copy
*  of this software and associated documentation files (the ""Software""), to deal
*  in the Software without restriction, including without limitation the rights
*  to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
*  copies of the Software, and to permit persons to whom the Software is
*  furnished to do so, subject to the following conditions:
*
*  The above copyright notice and this permission notice shall be included in
*  all copies or substantial portions of the Software.
*
*  THE SOFTWARE IS PROVIDED *AS IS*, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
*  IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
*  FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
*  AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
*  LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
*  OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
*  THE SOFTWARE.
*/
"use strict";

import "core-js/stable";
import "./../style/visual.less";
import powerbi from "powerbi-visuals-api";
import VisualConstructorOptions = powerbi.extensibility.visual.VisualConstructorOptions;
import VisualUpdateOptions = powerbi.extensibility.visual.VisualUpdateOptions;
import IVisual = powerbi.extensibility.visual.IVisual;
import EnumerateVisualObjectInstancesOptions = powerbi.EnumerateVisualObjectInstancesOptions;
import VisualObjectInstance = powerbi.VisualObjectInstance;
import DataView = powerbi.DataView;
import VisualObjectInstanceEnumerationObject = powerbi.VisualObjectInstanceEnumerationObject;

import { VisualSettings } from "./settings";
export class Visual implements IVisual {
    private target: HTMLElement;
    private updateCount: number;
    private settings: VisualSettings;
    private textNode: Text;
    private tableNode: HTMLElement;

    private prevValues: Map<string, string> = new Map<string, string>();
    private currValues: Map<string, string> = new Map<string, string>();
    private prevValue: string | number | boolean;
    private currValue: string | number | boolean;

    constructor(options: VisualConstructorOptions) {
        console.log('Visual constructor', options);
        this.target = options.element;
        this.tableNode = this.target.appendChild(document.createElement("table"));
        this.tableNode.style.border = "1px solid black";
        this.tableNode.style.borderCollapse = "collapse";
        this.updateCount = 0;
        if (typeof document !== "undefined") {
            // const new_p: HTMLElement = document.createElement("p");
            // new_p.appendChild(document.createTextNode("Update count:"));
            // const new_em: HTMLElement = document.createElement("em");
            // this.textNode = document.createTextNode(this.updateCount.toString());
            // new_em.appendChild(this.textNode);
            // new_p.appendChild(new_em);
            // this.target.appendChild(new_p);
        }
    }

    public update(options: VisualUpdateOptions) {
        //this.settings = Visual.parseSettings(options && options.dataViews && options.dataViews[0]);
        console.log('Visual update', options);
        if (typeof this.textNode !== "undefined") {
            this.textNode.textContent = (this.updateCount++).toString();
        }

        if (options.type !== powerbi.VisualUpdateType.Data) {
            return;
        }

        let map = new Map<string, string>();

        for (let i = 0; i < options.dataViews[0].categorical.categories.length; ++i) {
            const name = options.dataViews[0].categorical.categories[i].source.displayName;
            const value = options.dataViews[0].categorical.categories[i].values[0].valueOf();

            map.set(name, value.toString());
        }

        if (this.prevValues.size === 0) {
            this.prevValues = map;
        } else if (this.currValues.size === 0) {
            this.currValues = map;
        } else {
            this.prevValues = this.currValues;
            this.currValues = map;
        }

        if (this.prevValues.size > 0 && this.currValues.size > 0) {
            let s: string = "";
            this.tableNode.innerHTML = "";
            for (let k of this.prevValues.keys()) {
                const tr = this.tableNode.appendChild(document.createElement("tr"));
                let td = tr.appendChild(document.createElement("td"));
                td.appendChild(document.createTextNode(k));
                td.style.border = "1px solid black";
                td.style.borderCollapse = "collapse";
                td.onclick = () => {
                    navigator.clipboard.writeText(td.childNodes[0].textContent);
                };

                let td1 = tr.appendChild(document.createElement("td"));
                td1.appendChild(document.createTextNode(this.prevValues.get(k)));
                td1.style.border = "1px solid black";
                td1.style.borderCollapse = "collapse";
                td1.onclick = () => {
                    navigator.clipboard.writeText(td1.childNodes[0].textContent);
                };

                const td2 = tr.appendChild(document.createElement("td"));
                td2.appendChild(document.createTextNode(this.currValues.get(k)));
                td2.style.border = "1px solid black";
                td2.style.borderCollapse = "collapse";
                td2.onclick = () => {
                    navigator.clipboard.writeText(td2.childNodes[0].textContent);
                };
            }

            // this.prevValues.forEach((v, k) => {
            //     s += k + " " + v;
            // });
            // this.currValues.forEach((v, k) => {
            //     s += k + " " + v;
            // });
            //s += "</table>";
            //this.textNode.textContent = s;
        }
    }

    private static parseSettings(dataView: DataView): VisualSettings {
        return VisualSettings.parse(dataView) as VisualSettings;
    }

    /**
     * This function gets called for each of the objects defined in the capabilities files and allows you to select which of the
     * objects and properties you want to expose to the users in the property pane.
     *
     */
    public enumerateObjectInstances(options: EnumerateVisualObjectInstancesOptions): VisualObjectInstance[] | VisualObjectInstanceEnumerationObject {
        return VisualSettings.enumerateObjectInstances(this.settings || VisualSettings.getDefault(), options);
    }
}