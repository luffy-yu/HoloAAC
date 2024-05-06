import os

import cv2
from inference import ObjectDetection
import pandas as pd
import numpy as np


def evaluate(base_folder):
    output_folder = os.path.join(os.path.dirname(base_folder), 'output')
    if not os.path.exists(output_folder):
        os.mkdir(output_folder)

    count = 125

    labels = """
        beans
        cake
        candy
        cereal
        chips
        chocolate
        coffee
        corn
        fish
        flour
        honey
        jam
        juice
        milk
        nuts
        oil
        pasta
        rice
        soda
        spices
        sugar
        tea
        tomato_sauce
        vinegar
        water
        """

    labels = list(map(str.strip, labels.strip().split('\n')))
    print(labels)

    flags = []

    for i in range(1, count + 1):
        name = f'Image - {i}.jpeg'
        filename = os.path.join(base_folder, name)
        output_name = os.path.join(output_folder, name)

        image = cv2.imread(filename)
        result_dict, result_image = ObjectDetection.run(image, draw_image=True, standalone=None)
        flag = 'o'
        if result_dict:
            cv2.imwrite(output_name, result_image)
            if labels[int((i - 1) / 5)] in result_dict:
                flag = '√'
            else:
                flag = '×'
        flags.append(flag)

    columns = ['front', 'right', 'behind', 'left', 'top']

    data = np.array(flags).reshape((-1, 5))
    df = pd.DataFrame(data, columns=columns)
    df.index = labels
    # summary for each class
    df['rate'] = df.apply(lambda x: x.tolist().count('√') / 5., axis=1)
    # summary for each side
    df = df.append(pd.DataFrame([[0.] * 5], columns=columns, index=['summary']))

    right_count = 0
    for col in columns:
        num = df[col].tolist().count('√')
        df.loc['summary', col] = num / len(labels)
        right_count += num

    # summary of summary
    df.loc['summary', 'rate'] = right_count / count

    print(df)

    excel_name = os.path.join(output_folder, 'report.xls')
    df.to_excel(excel_name)


if __name__ == '__main__':
    folder = r'D:\Study\Resource\CustomDataset\Images'
    evaluate(folder)
