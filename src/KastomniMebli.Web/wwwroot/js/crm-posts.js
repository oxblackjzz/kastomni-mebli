// Підготовка фото для постів у браузері: поворот за EXIF, обрізка під Instagram (4:5 … 1.91:1),
// зменшення до 1440 px, JPEG. EXIF (зокрема GPS) при цьому зникає. Повертає байти JPEG у .NET.
window.kmPosts = {
    async prepare(input, index) {
        const file = input.files[index];
        let bitmap;
        try {
            bitmap = await createImageBitmap(file, { imageOrientation: 'from-image' });
        } catch {
            throw new Error('Браузер не зміг відкрити фото. Збережіть його як JPEG і спробуйте ще раз.');
        }

        const minRatio = 4 / 5;   // найвужче — вертикальне 4:5
        const maxRatio = 1.91;    // найширше — горизонтальне 1.91:1
        let sx = 0, sy = 0, sw = bitmap.width, sh = bitmap.height;
        const ratio = sw / sh;
        if (ratio < minRatio) {
            sh = Math.round(sw / minRatio);
            sy = Math.round((bitmap.height - sh) / 2);
        } else if (ratio > maxRatio) {
            sw = Math.round(sh * maxRatio);
            sx = Math.round((bitmap.width - sw) / 2);
        }

        const scale = Math.min(1, 1440 / sw);
        const canvas = document.createElement('canvas');
        canvas.width = Math.round(sw * scale);
        canvas.height = Math.round(sh * scale);
        const ctx = canvas.getContext('2d');
        ctx.fillStyle = '#fff';
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.drawImage(bitmap, sx, sy, sw, sh, 0, 0, canvas.width, canvas.height);
        bitmap.close?.();

        const blob = await new Promise((resolve) => canvas.toBlob(resolve, 'image/jpeg', 0.88));
        return new Uint8Array(await blob.arrayBuffer());
    },

    count(input) {
        return input.files ? input.files.length : 0;
    },
};
