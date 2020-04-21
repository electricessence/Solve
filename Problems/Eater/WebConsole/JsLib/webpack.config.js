const path = require("path");

module.exports = {

    module: {
        rules: [
            {
                test: /\.(js|jsx)$/,
                exclude: /node_modules/,
                use: {
                    loader: "babel-loader"
                }
            }
        ]
    },

    devtool: 'source-map',

    output: {
        path: path.resolve(__dirname, '../wwwroot/js'),
        filename: "site.js",
        library: "Site"
    }

};