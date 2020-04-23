const path = require("path");
//const HtmlWebpackPlugin = require("html-webpack-plugin");
const { CleanWebpackPlugin } = require("clean-webpack-plugin");
const MiniCssExtractPlugin = require("mini-css-extract-plugin");

const webpackPath = 'js/webpack/';
const mainPath = '[name].js';
const chunkPath = '[name]/[chunkhash].js';

module.exports = {
    entry: "./Client/index.ts",
    devtool: 'source-map',
    output: {
        path: path.resolve(__dirname, 'wwwroot', webpackPath),
        filename: (chunkData) => chunkData.chunk.name === 'main' ? mainPath : chunkPath,
        chunkFilename: chunkPath,
        library: 'main'
    },
    // optimization: {
    //     splitChunks: {
    //         maxInitialRequests: Infinity,
    //         minSize: 0,
    //         cacheGroups: {
    //             modules: {
    //                 name: 'modules',
    //                 test: /[\\/]Client[\\/]modules[\\/]/,
    //                 chunks: 'all',
    //             },
    //             vendor: {
    //                 name: 'vendor',
    //                 test: /[\\/]node_modules[\\/]/,
    //                 chunks: 'all',
    //                 name: (module) => {
    //                     // get the name. E.g. node_modules/packageName/not/this/part.js
    //                     // or node_modules/packageName
    //                     const packageName = module.context.match(/[\\/]node_modules[\\/](.*?)([\\/]|$)/)[1];

    //                     // npm package names are URL-safe, but some servers don't like @ symbols
    //                     return 'npm/' + packageName.replace('@', '');
    //                 },
    //                 enforce: true
    //             },
    //         }
    //     }
    // },
    resolve: {
        extensions: [".js", ".ts"]
    },
    module: {
        rules: [
            {
                test: /\.js$/,
                use: ["source-map-loader"],
                enforce: "pre"
            },
            {
                test: /\.ts$/,
                use: "ts-loader"
            },
            {
                test: /\.less/,
                use: "less-loader"
            },
            {
                test: /\.css$/,
                use: [MiniCssExtractPlugin.loader, "css-loader"]
            }
        ]
    },
    plugins: [
        new CleanWebpackPlugin(),
        //new HtmlWebpackPlugin({
        //    template: "./src/index.html"
        //}),
        new MiniCssExtractPlugin({
            filename: "css/webpack/[name].[chunkhash].css"
        })
    ]
};