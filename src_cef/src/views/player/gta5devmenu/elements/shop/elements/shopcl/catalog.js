// Каталог донат-одежды. id, тип и цена должны совпадать с DonateClothesList
// в dotnet/resources/NeptuneEvo/Chars/Donate.cs (server.donate.buy.clothes).
import img0 from '../../../../assets/clothes/0.png';
import img1 from '../../../../assets/clothes/1.png';
import img2 from '../../../../assets/clothes/2.png';
import img3 from '../../../../assets/clothes/3.png';
import img4 from '../../../../assets/clothes/4.png';
import img5 from '../../../../assets/clothes/5.png';
import img6 from '../../../../assets/clothes/6.png';
import img7 from '../../../../assets/clothes/7.png';
import img8 from '../../../../assets/clothes/8.png';
import img9 from '../../../../assets/clothes/9.png';
import img10 from '../../../../assets/clothes/10.png';
import img11 from '../../../../assets/clothes/11.png';
import img12 from '../../../../assets/clothes/12.png';
import img13 from '../../../../assets/clothes/13.png';
import img14 from '../../../../assets/clothes/14.png';
import img15 from '../../../../assets/clothes/15.png';
import img16 from '../../../../assets/clothes/16.png';
import img17 from '../../../../assets/clothes/17.png';
import img18 from '../../../../assets/clothes/18.png';
import img19 from '../../../../assets/clothes/19.png';
import img20 from '../../../../assets/clothes/20.png';
import img21 from '../../../../assets/clothes/21.png';
import img22 from '../../../../assets/clothes/22.png';
import img23 from '../../../../assets/clothes/23.png';
import img24 from '../../../../assets/clothes/24.png';
import img25 from '../../../../assets/clothes/25.png';
import img26 from '../../../../assets/clothes/26.png';
import img27 from '../../../../assets/clothes/27.png';
import img28 from '../../../../assets/clothes/28.png';
import img29 from '../../../../assets/clothes/29.png';
import img30 from '../../../../assets/clothes/30.png';
import img31 from '../../../../assets/clothes/31.png';
import img32 from '../../../../assets/clothes/32.png';
import img33 from '../../../../assets/clothes/33.png';
import img34 from '../../../../assets/clothes/34.png';
import img35 from '../../../../assets/clothes/35.png';

export const clothesList = [
    { id: 0, type: "Mask", male: true, price: 30000, name: "Борода", img: img0 },
    { id: 1, type: "Mask", male: true, price: 1000, name: "Маска крысы", img: img1 },
    { id: 2, type: "Mask", male: true, price: 2000, name: "Неоновая маска", img: img2 },
    { id: 3, type: "Mask", male: true, price: 35000, name: "Маска Marshmallow", img: img3 },
    { id: 4, type: "Glasses", male: true, price: 10000, name: "Неоновые очки", img: img4 },
    { id: 5, type: "Hat", male: true, price: 30000, name: "Неоновый шлем", img: img5 },
    { id: 6, type: "Hat", male: true, price: 1500, name: "Фуражка адмирала", img: img6 },
    { id: 7, type: "Hat", male: true, price: 4200, name: "Модная повязка", img: img7 },
    { id: 8, type: "Leg", male: true, price: 25000, name: "Неоновые штаны", img: img8 },
    { id: 9, type: "Leg", male: true, price: 20000, name: "Яркие штаны", img: img9 },
    { id: 10, type: "Leg", male: true, price: 20000, name: "Штаны с подсветкой", img: img10 },
    { id: 11, type: "Leg", male: true, price: 5000, name: "Шорты Supreme", img: img11 },
    { id: 12, type: "Leg", male: true, price: 30000, name: "Штаны GUCCI", img: img12 },
    { id: 13, type: "Feet", male: true, price: 25000, name: "Неоновая обувь", img: img13 },
    { id: 14, type: "Feet", male: true, price: 3000, name: "Ласты", img: img14 },
    { id: 15, type: "Top", male: true, price: 15000, name: "Неоновый верх", img: img15 },
    { id: 16, type: "Top", male: true, price: 10000, name: "Яркий верх", img: img16 },
    { id: 17, type: "Top", male: true, price: 10000, name: "Верх с подсветкой", img: img17 },
    { id: 18, type: "Top", male: true, price: 30000, name: "Толстовка GUCCI", img: img18 },
    { id: 19, type: "Top", male: true, price: 5000, name: "Модная футболка", img: img19 },
    { id: 20, type: "Mask", male: false, price: 1000, name: "Маска крысы", img: img20 },
    { id: 21, type: "Mask", male: false, price: 2000, name: "Неоновая маска", img: img21 },
    { id: 22, type: "Mask", male: false, price: 35000, name: "Маска Marshmallow", img: img22 },
    { id: 23, type: "Glasses", male: false, price: 10000, name: "Неоновые очки", img: img23 },
    { id: 24, type: "Hat", male: false, price: 30000, name: "Неоновый шлем", img: img24 },
    { id: 25, type: "Hat", male: false, price: 1500, name: "Фуражка адмирала", img: img25 },
    { id: 26, type: "Leg", male: false, price: 25000, name: "Неоновые штаны", img: img26 },
    { id: 27, type: "Leg", male: false, price: 20000, name: "Яркие штаны", img: img27 },
    { id: 28, type: "Leg", male: false, price: 20000, name: "Штаны с подсветкой", img: img28 },
    { id: 29, type: "Feet", male: false, price: 10000, name: "Неоновая обувь", img: img29 },
    { id: 30, type: "Feet", male: false, price: 3000, name: "Ласты", img: img30 },
    { id: 31, type: "Top", male: false, price: 15000, name: "Неоновый верх", img: img31 },
    { id: 32, type: "Top", male: false, price: 10000, name: "Яркий верх", img: img32 },
    { id: 33, type: "Top", male: false, price: 10000, name: "Верх с подсветкой", img: img33 },
    { id: 34, type: "Top", male: false, price: 2000, name: "Модная футболка", img: img34 },
    { id: 35, type: "Top", male: false, price: 4000, name: "Пошлая футболка", img: img35 },
];

// Разделы меню (pageload в gta5devmenu/index.svelte)
export const categories = [
    { page: 1, title: "Маски", type: "Mask" },
    { page: 2, title: "Головные уборы", type: "Hat" },
    { page: 3, title: "Аксессуары", type: "Glasses" },
    { page: 4, title: "Верхняя одежда", type: "Top" },
    { page: 5, title: "Бронежилеты", type: "Armor" },
    { page: 6, title: "Нижняя одежда", type: "Undershirt" },
    { page: 7, title: "Штаны", type: "Leg" },
    { page: 8, title: "Обувь", type: "Feet" },
];

export const getRarity = (price) => {
    if (price >= 30000) return 5;
    if (price >= 15000) return 4;
    if (price >= 5000) return 3;
    if (price >= 2500) return 2;
    return 1;
}
