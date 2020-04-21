import { Mesh, MeshLambertMaterial, SpriteMaterial, Sprite, SphereGeometry, Texture } from "three";
// function for drawing rounded rectangles
function roundRect(c, x, y, w, h, r) {
    c.beginPath();
    c.moveTo(x + r, y);
    c.lineTo(x + w - r, y);
    c.quadraticCurveTo(x + w, y, x + w, y + r);
    c.lineTo(x + w, y + h - r);
    c.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
    c.lineTo(x + r, y + h);
    c.quadraticCurveTo(x, y + h, x, y + h - r);
    c.lineTo(x, y + r);
    c.quadraticCurveTo(x, y, x + r, y);
    c.closePath();
    c.fill();
    c.stroke();
}
export function makeTextSprite(message, parameters) {
    if (parameters === undefined)
        parameters = {};
    var fontface = parameters.hasOwnProperty("fontface") ?
        parameters["fontface"] : "Arial";
    var fontsize = parameters.hasOwnProperty("fontsize") ?
        parameters["fontsize"] : 18;
    var borderThickness = parameters.hasOwnProperty("borderThickness") ?
        parameters["borderThickness"] : 4;
    var borderColor = parameters.hasOwnProperty("borderColor") ?
        parameters["borderColor"] : { r: 0, g: 0, b: 0, a: 1.0 };
    var backgroundColor = parameters.hasOwnProperty("backgroundColor") ?
        parameters["backgroundColor"] : { r: 255, g: 255, b: 255, a: 1.0 };
    var canvas = document.createElement('canvas');
    var context = canvas.getContext('2d');
    if (context == null)
        throw "2d context not found.";
    context.font = "Bold " + fontsize + "px " + fontface;
    // get size data (height depends only on font size)
    var metrics = context.measureText(message);
    var textWidth = metrics.width;
    // background color
    context.fillStyle = "rgba(" + backgroundColor.r + "," + backgroundColor.g + ","
        + backgroundColor.b + "," + backgroundColor.a + ")";
    // border color
    context.strokeStyle = "rgba(" + borderColor.r + "," + borderColor.g + ","
        + borderColor.b + "," + borderColor.a + ")";
    context.lineWidth = borderThickness;
    roundRect(context, borderThickness / 2, borderThickness / 2, textWidth + borderThickness, fontsize * 1.4 + borderThickness, 6);
    // 1.4 is extra height factor for text below baseline: g,j,p,q.
    // text color
    context.fillStyle = "rgba(0, 0, 0, 1.0)";
    context.fillText(message, borderThickness, fontsize + borderThickness);
    // canvas contents will be used for a texture
    var texture = new Texture(canvas);
    texture.needsUpdate = true;
    var spriteMaterial = new SpriteMaterial({ map: texture });
    var sprite = new Sprite(spriteMaterial);
    sprite.scale.set(100, 50, 1.0);
    return sprite;
}
export function addSphereTo(parent, color, x, y, z, radius, segments = 16, rings = 16) {
    // create the sphere's material
    var sphereMaterial = new MeshLambertMaterial({ color: color });
    // create a new mesh with
    // sphere geometry - we will cover
    // the sphereMaterial next!
    var sphere = new Mesh(new SphereGeometry(radius, segments, rings), sphereMaterial);
    sphere.position.set(x, y, z);
    // add the sphere to the scene
    parent.add(sphere);
    return sphere;
}
;
export function addMarkerTo(parent, x, y, z, text) {
    var sprite = makeTextSprite(text ? text : "   ", { fontsize: 12, backgroundColor: { r: 100, g: 0, b: 0, a: 1 }, borderThickness: 2 });
    var p = sprite.position;
    p.x = x;
    p.y = y;
    p.z = z;
    parent.add(sprite);
    return sprite;
}
//export function make3dText(message: string, faceColor: number = 0x000000, oulineColor: number = faceColor) : Mesh & IDisposable {
//	// add 3D text
//	var materialFront = new MeshBasicMaterial({ color: faceColor });
//	var materialSide = new MeshBasicMaterial({ color: oulineColor });
//	var materialArray = [materialFront, materialSide];
//	var textGeom = new TextGeometry(message,
//	{
//		size: 4, height: 0.5, curveSegments: 3,
//		font: new Font({ family: "helvetiker", weight: "normal", style: "normal" }),
//		bevelThickness: 0, bevelSize: 0, bevelEnabled: false
//	});
//	// font: helvetiker, gentilis, droid sans, droid serif, optimer
//	// weight: normal, bold
//	var textMaterial = new MeshFaceMaterial(materialArray);
//	var textMesh = new Mesh(textGeom, textMaterial);
//	textGeom.computeBoundingBox();
//	//@ts-ignore
//	textMesh.dispose = textMesh.dispose || dispose;
//	//@ts-ignore
//	return textMesh;
//	function dispose () {
//		if (textMesh.parent)
//			textMesh.parent.remove(textMesh);
//		textGeom.dispose();
//		materialFront.dispose();
//		materialSide.dispose();
//		//renderer.deallocateTexture( materialFront );
//		//renderer.deallocateTexture( materialSide );
//	}
//}
//# sourceMappingURL=Utils.js.map