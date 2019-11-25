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
    private leftButtonNode: HTMLElement;
    private rightButtonNode: HTMLElement;

    private selectedPoint: Map<string, string> = new Map<string, string>();
    private leftValues: Map<string, string> = new Map<string, string>();
    private rightValues: Map<string, string> = new Map<string, string>();

    constructor(options: VisualConstructorOptions) {
        this.target = options.element;

        // title
        this.target.appendChild(document.createElement("b")).innerText = "Perf Regression Tool";

        // main display table
        this.tableNode = this.target.appendChild(document.createElement("table"));
        this.tableNode.style.border = "1px solid black";
        this.tableNode.style.borderCollapse = "collapse";
        this.tableNode.innerHTML = "Click 2 points";

        // Set left column button
        this.leftButtonNode = this.target.appendChild(document.createElement("button"));
        this.leftButtonNode.innerText = "Set left point";
        this.leftButtonNode.onclick = () => {
            this.leftValues = new Map<string, string>();
            // copy selected point into left column
            for (let i of this.selectedPoint.keys()) {
                this.leftValues.set(i, this.selectedPoint.get(i));
            }
            // update table display
            this.display();
        };

        // Set right column button
        this.rightButtonNode = this.target.appendChild(document.createElement("button"));
        this.rightButtonNode.innerText = "Set right point";
        this.rightButtonNode.onclick = () => {
            this.rightValues = new Map<string, string>();
            // copy selected point into right column
            for (let i of this.selectedPoint.keys()) {
                this.rightValues.set(i, this.selectedPoint.get(i));
            }
            // update table display
            this.display();
        };
    }

    public update(options: VisualUpdateOptions) {
        //this.settings = Visual.parseSettings(options && options.dataViews && options.dataViews[0]);
        if (typeof this.textNode !== "undefined") {
            this.textNode.textContent = (this.updateCount++).toString();
        }

        if (options.type !== powerbi.VisualUpdateType.Data) {
            return;
        }

        for (let i = 0; i < options.dataViews[0].categorical.categories.length; ++i) {
            const name = options.dataViews[0].categorical.categories[i].source.displayName;
            const value = options.dataViews[0].categorical.categories[i].values[0].valueOf();

            this.selectedPoint.set(name, value.toString());
        }
    }

    private static parseSettings(dataView: DataView): VisualSettings {
        return VisualSettings.parse(dataView) as VisualSettings;
    }

    private static setOnClick(td: HTMLTableDataCellElement) {
        td.onclick = () => {
            navigator.clipboard.writeText(td.childNodes[0].textContent);
            td.style.backgroundColor = "lime";
            setTimeout(() => {
                td.style.backgroundColor = "white";
            }, 1000);
        };
    }

    private display() {
        if (this.leftValues.size > 0 || this.rightValues.size > 0) {
            this.tableNode.innerHTML = "";
            let map = this.leftValues.size > 0 ? this.leftValues : this.rightValues;
            for (let k of map.keys()) {
                // Row name: e.g. CPU %
                const tr = this.tableNode.appendChild(document.createElement("tr"));
                let td = tr.appendChild(document.createElement("td"));
                td.appendChild(document.createTextNode(k));
                td.style.border = "1px solid black";
                td.style.borderCollapse = "collapse";
                Visual.setOnClick(td);

                // Left point value
                let td1 = tr.appendChild(document.createElement("td"));
                td1.style.border = "1px solid black";
                td1.style.borderCollapse = "collapse";
                if (this.leftValues.has(k)) {
                    td1.appendChild(document.createTextNode(this.leftValues.get(k)));
                    Visual.setOnClick(td1);
                } else {
                    td1.innerText = "N/A";
                }

                // Right point value
                const td2 = tr.appendChild(document.createElement("td"));
                td2.style.border = "1px solid black";
                td2.style.borderCollapse = "collapse";
                if (this.rightValues.has(k)) {
                    td2.appendChild(document.createTextNode(this.rightValues.get(k)));
                    Visual.setOnClick(td2);
                } else {
                    td2.innerText = "N/A";
                }
            }
        }
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